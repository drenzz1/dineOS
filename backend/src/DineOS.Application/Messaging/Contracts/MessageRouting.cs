namespace DineOS.Application.Messaging.Contracts;

public static class MessageRouting
{
    public const string ExchangeName = "dineos.events";
    public const string DeadLetterExchangeName = "dineos.events.dlx";

    public const string OrderCreatedRoutingKey = "orders.created";
    public const string OrderCreatedQueueName = "dineos.orders.created.notifications";
    public const string OrderCreatedDeadLetterQueueName = "dineos.orders.created.notifications.dlq";
}
