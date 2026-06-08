using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Messaging;

public sealed class RabbitMqTopologyHostedService(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitOptions = options.Value;
        if (!rabbitOptions.Enabled)
        {
            logger.LogInformation("RabbitMQ topology declaration disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await connectionProvider.CreateChannelAsync(ct: stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, rabbitOptions, stoppingToken);

                logger.LogInformation(
                    "RabbitMQ topology declared: Exchange={ExchangeName} Queue={QueueName} DeadLetterQueue={DeadLetterQueueName}",
                    rabbitOptions.ExchangeName,
                    rabbitOptions.OrderCreatedQueueName,
                    rabbitOptions.OrderCreatedDeadLetterQueueName);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "RabbitMQ topology declaration failed; retrying in {RetryDelaySeconds}s",
                    rabbitOptions.ReconnectDelaySeconds);

                await DelayBeforeRetryAsync(rabbitOptions, stoppingToken);
            }
        }
    }

    private static async Task DelayBeforeRetryAsync(
        RabbitMqOptions rabbitOptions,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(rabbitOptions.ReconnectDelaySeconds), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
