using DineOS.Api.Hubs;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace DineOS.Api.Services;

public class OrderNotificationService(IHubContext<OrderUpdatesHub, IOrderClient> hubContext)
    : IOrderNotificationService
{
    public Task BroadcastOrderCreatedAsync(long tenantId, OrderDto order, CancellationToken ct = default)
    {
        var evt = new OrderCreatedEvent(
            order.Id,
            tenantId,
            order.OrderType,
            order.TableNumber,
            order.Status,
            order.Total,
            order.Notes,
            order.CreatedAt,
            order.Items
                .Select(i => new OrderItemPayload(i.Id, i.Name, i.Quantity, i.UnitPrice, i.Notes))
                .ToList());

        return hubContext.Clients
            .Group(OrderUpdatesHub.GroupName(tenantId))
            .OrderCreated(evt);
    }

    public Task BroadcastOrderStatusChangedAsync(long tenantId, long orderId, string oldStatus, string newStatus, CancellationToken ct = default)
    {
        var evt = new OrderStatusChangedEvent(orderId, tenantId, oldStatus, newStatus, DateTime.UtcNow);

        return hubContext.Clients
            .Group(OrderUpdatesHub.GroupName(tenantId))
            .OrderStatusChanged(evt);
    }
}
