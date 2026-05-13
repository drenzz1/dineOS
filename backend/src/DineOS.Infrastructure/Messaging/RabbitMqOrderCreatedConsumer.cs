using DineOS.Application.Messaging.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DineOS.Infrastructure.Messaging;

public sealed class RabbitMqOrderCreatedConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqOrderCreatedConsumer> logger) : BackgroundService
{
    private const string DeliveryAttemptHeader = "x-delivery-attempt";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitOptions = options.Value;
        if (!rabbitOptions.Enabled)
        {
            logger.LogInformation("RabbitMQ order-created consumer disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: rabbitOptions.PrefetchCount,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, args) =>
                    HandleDeliveryAsync(channel, args, stoppingToken);

                var consumerTag = await channel.BasicConsumeAsync(
                    rabbitOptions.OrderCreatedQueueName,
                    autoAck: false,
                    consumer,
                    stoppingToken);

                logger.LogInformation(
                    "RabbitMQ order-created consumer started: Queue={QueueName} ConsumerTag={ConsumerTag}",
                    rabbitOptions.OrderCreatedQueueName, consumerTag);

                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "RabbitMQ order-created consumer failed; retrying in {RetryDelaySeconds}s",
                    rabbitOptions.ReconnectDelaySeconds);

                await DelayBeforeReconnectAsync(rabbitOptions, stoppingToken);
            }
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken ct)
    {
        var body = args.Body.ToArray();

        try
        {
            var message = JsonSerializer.Deserialize<OrderCreatedMessage>(body, JsonOptions)
                ?? throw new JsonException("OrderCreated message body was empty.");

            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<OrderCreatedMessageHandler>();
            await handler.HandleAsync(message, ct);

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);

            logger.LogInformation(
                "RabbitMQ event consumed: MessageId={MessageId} EventType={EventType} RoutingKey={RoutingKey} Redelivered={Redelivered}",
                message.MessageId, nameof(OrderCreatedMessage), args.RoutingKey, args.Redelivered);
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "RabbitMQ event payload invalid; dead-lettering: DeliveryTag={DeliveryTag} RoutingKey={RoutingKey}",
                args.DeliveryTag, args.RoutingKey);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(channel, args, body, ex, ct);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        byte[] body,
        Exception exception,
        CancellationToken ct)
    {
        var rabbitOptions = options.Value;
        var nextAttempt = GetDeliveryAttempt(args.BasicProperties) + 1;

        if (nextAttempt <= rabbitOptions.MaxRetryAttempts)
        {
            try
            {
                await RepublishForRetryAsync(channel, args, body, nextAttempt, ct);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);

                logger.LogWarning(
                    exception,
                    "RabbitMQ event handling failed; republished for retry: MessageId={MessageId} RoutingKey={RoutingKey} Attempt={Attempt} MaxRetryAttempts={MaxRetryAttempts}",
                    args.BasicProperties.MessageId, args.RoutingKey, nextAttempt, rabbitOptions.MaxRetryAttempts);
            }
            catch (Exception retryException)
            {
                logger.LogError(
                    retryException,
                    "RabbitMQ retry publish failed; requeueing original delivery: MessageId={MessageId} RoutingKey={RoutingKey}",
                    args.BasicProperties.MessageId, args.RoutingKey);
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }

            return;
        }

        logger.LogError(
            exception,
            "RabbitMQ event handling failed after retries; dead-lettering: MessageId={MessageId} RoutingKey={RoutingKey} Attempts={Attempts}",
            args.BasicProperties.MessageId, args.RoutingKey, nextAttempt);
        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
    }

    private async Task RepublishForRetryAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        byte[] body,
        int attempt,
        CancellationToken ct)
    {
        var rabbitOptions = options.Value;
        var headers = CopyHeaders(args.BasicProperties.Headers);
        headers[DeliveryAttemptHeader] = attempt;

        var properties = new BasicProperties(args.BasicProperties)
        {
            Headers = headers,
            Persistent = true
        };

        await channel.BasicPublishAsync(
            rabbitOptions.ExchangeName,
            args.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }

    private static Dictionary<string, object?> CopyHeaders(IDictionary<string, object?>? headers) =>
        headers is null
            ? []
            : headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    private static int GetDeliveryAttempt(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue(DeliveryAttemptHeader, out var value))
            return 0;

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            byte b => b,
            short s => s,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }

    private static async Task DelayBeforeReconnectAsync(
        RabbitMqOptions options,
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(options.ReconnectDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
