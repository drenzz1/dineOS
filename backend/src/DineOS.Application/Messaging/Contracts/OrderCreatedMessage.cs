namespace DineOS.Application.Messaging.Contracts;

public record OrderCreatedMessage(
    string MessageId,
    long OrderId,
    long TenantId,
    string OrderType,
    int? TableNumber,
    string Status,
    decimal Total,
    string? Notes,
    DateTime CreatedAt,
    DateTime OccurredAt,
    IReadOnlyList<OrderItemMessage> Items) : IMessage;

public record OrderItemMessage(
    long Id,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes);
