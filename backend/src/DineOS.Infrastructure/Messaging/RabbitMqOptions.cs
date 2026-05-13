using DineOS.Application.Messaging.Contracts;

namespace DineOS.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; } = true;
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "dineos";
    public string Password { get; set; } = "dineos_dev";
    public string VirtualHost { get; set; } = "/";
    public string ClientProvidedName { get; set; } = "dineos-api";

    public string ExchangeName { get; set; } = MessageRouting.ExchangeName;
    public string DeadLetterExchangeName { get; set; } = MessageRouting.DeadLetterExchangeName;
    public string OrderCreatedRoutingKey { get; set; } = MessageRouting.OrderCreatedRoutingKey;
    public string OrderCreatedQueueName { get; set; } = MessageRouting.OrderCreatedQueueName;
    public string OrderCreatedDeadLetterQueueName { get; set; } = MessageRouting.OrderCreatedDeadLetterQueueName;

    public ushort PrefetchCount { get; set; } = 1;
    public int MaxRetryAttempts { get; set; } = 3;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int NetworkRecoveryIntervalSeconds { get; set; } = 10;
}
