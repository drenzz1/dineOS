using RabbitMQ.Client;

namespace DineOS.Infrastructure.Messaging;

internal static class RabbitMqTopology
{
    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(
            options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var orderQueueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = options.DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = options.OrderCreatedRoutingKey
        };

        await channel.QueueDeclareAsync(
            options.OrderCreatedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: orderQueueArguments,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            options.OrderCreatedDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            options.OrderCreatedQueueName,
            options.ExchangeName,
            options.OrderCreatedRoutingKey,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            options.OrderCreatedDeadLetterQueueName,
            options.DeadLetterExchangeName,
            options.OrderCreatedRoutingKey,
            cancellationToken: ct);
    }
}
