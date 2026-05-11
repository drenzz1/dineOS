using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IOrderNotificationService
{
    Task BroadcastOrderCreatedAsync(long tenantId, OrderDto order, CancellationToken ct = default);
    Task BroadcastOrderStatusChangedAsync(long tenantId, long orderId, string oldStatus, string newStatus, CancellationToken ct = default);
}
