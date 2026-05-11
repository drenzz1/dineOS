using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Kitchen;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class KitchenService(
    AppDbContext db,
    ICurrentUserService currentUserService,
    IOrderNotificationService notificationService,
    IValidator<UpdateKitchenOrderStatusRequest> statusValidator,
    ILogger<KitchenService> logger) : IKitchenService
{
    private static readonly OrderStatus[] ActiveKitchenStatuses =
        [OrderStatus.New, OrderStatus.InProgress, OrderStatus.Ready];

    public async Task<ServiceResult<List<OrderDto>>> GetKitchenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => ActiveKitchenStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<OrderDto>>.Ok(orders.Select(ToDto).ToList(), "Kitchen order queue");
    }

    public async Task<ServiceResult<OrderDto>> UpdateOrderStatusAsync(
        long id,
        UpdateKitchenOrderStatusRequest request,
        CancellationToken ct = default)
    {
        var validation = await statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<OrderDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return ServiceResult<OrderDto>.NotFound($"Order {id} not found.");

        var previous = order.Status.ToString();
        order.Status = Enum.Parse<OrderStatus>(request.Status);
        await db.SaveChangesAsync(ct);

        await notificationService.BroadcastOrderStatusChangedAsync(
            order.TenantId, order.Id, previous, request.Status, ct);

        logger.LogInformation(
            "Kitchen order status changed: OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId} Previous={Previous} Current={Current}",
            order.Id, order.TenantId, currentUserService.UserId, previous, request.Status);

        return ServiceResult<OrderDto>.Ok(ToDto(order), $"Order {id} status updated to {request.Status}.");
    }

    public async Task<ServiceResult<KitchenQueueSummaryDto>> GetQueueSummaryAsync(CancellationToken ct = default)
    {
        var counts = await db.Orders
            .AsNoTracking()
            .Where(o => ActiveKitchenStatuses.Contains(o.Status))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(OrderStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        var summary = new KitchenQueueSummaryDto(
            Pending:    Count(OrderStatus.New),
            InProgress: Count(OrderStatus.InProgress),
            Ready:      Count(OrderStatus.Ready));

        return ServiceResult<KitchenQueueSummaryDto>.Ok(summary, "Kitchen queue summary");
    }

    private static OrderDto ToDto(Order o) => new()
    {
        Id          = o.Id,
        OrderType   = o.OrderType,
        TableNumber = o.TableNumber,
        Status      = o.Status.ToString(),
        Total       = o.Total,
        Notes       = o.Notes,
        TenantId    = o.TenantId,
        CreatedAt   = o.CreatedAt,
        Items       = o.Items.Select(i => new OrderItemDto
        {
            Id        = i.Id,
            Name      = i.Name,
            Quantity  = i.Quantity,
            UnitPrice = i.UnitPrice,
            Notes     = i.Notes,
        }).ToList()
    };
}
