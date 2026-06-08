using DineOS.Application.Interfaces.Messaging;
using DineOS.Application.Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text.Json;

namespace DineOS.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqMessagePublisher> logger) : IMessagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> TryPublishAsync<TMessage>(
        TMessage message,
        string routingKey,
        CancellationToken ct = default)
        where TMessage : IMessage
    {
        var rabbitOptions = options.Value;
        if (!rabbitOptions.Enabled)
        {
            logger.LogDebug(
                "RabbitMQ publishing disabled: MessageId={MessageId} EventType={EventType} RoutingKey={RoutingKey}",
                message.MessageId, typeof(TMessage).Name, routingKey);
            return false;
        }

        await using var channel = await connectionProvider.CreateChannelAsync(publisherConfirms: true, ct: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = message.MessageId,
            Type = typeof(TMessage).Name,
            AppId = "dineos-api",
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-event-type"] = typeof(TMessage).Name,
                ["x-delivery-attempt"] = 0
            }
        };

        try
        {
            // With publisher confirmations enabled, this awaits the broker ack and
            // throws if the message is nacked or returned unroutable (mandatory).
            await channel.BasicPublishAsync(
                rabbitOptions.ExchangeName,
                routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Unconfirmed / unroutable / broker-down publish. Report failure
            // (return false) instead of a phantom success so the caller's
            // in-process fallback (e.g. OrderService -> SignalR broadcast) engages
            // rather than the event silently vanishing.
            logger.LogWarning(
                ex,
                "RabbitMQ publish not confirmed; treating as failed: MessageId={MessageId} EventType={EventType} RoutingKey={RoutingKey}",
                message.MessageId, typeof(TMessage).Name, routingKey);
            return false;
        }

        logger.LogInformation(
            "RabbitMQ event published: MessageId={MessageId} EventType={EventType} RoutingKey={RoutingKey}",
            message.MessageId, typeof(TMessage).Name, routingKey);

        return true;
    }
}
