namespace DineOS.Application.Notifications;

public record OrderItemPayload(
    long Id,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes);

public record OrderCreatedEvent(
    long OrderId,
    long TenantId,
    string OrderType,
    int? TableNumber,
    string Status,
    decimal Total,
    string? Notes,
    DateTime CreatedAt,
    List<OrderItemPayload> Items);

public record OrderStatusChangedEvent(
    long OrderId,
    long TenantId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt);

/// <summary>Strongly typed SignalR client interface — one method per event the server pushes.</summary>
public interface IOrderClient
{
    Task OrderCreated(OrderCreatedEvent evt);
    Task OrderStatusChanged(OrderStatusChangedEvent evt);
}
