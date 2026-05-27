using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Messaging;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Messaging.Contracts;
using DineOS.Application.Orders;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DineOS.Infrastructure.Services;

public class OrderService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<CreateOrderRequest> createValidator,
    IValidator<UpdateOrderStatusRequest> statusValidator,
    IMessagePublisher messagePublisher,
    IOrderNotificationService notificationService,
    IOrderMetrics orderMetrics,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<ServiceResult<List<OrderDto>>> GetOrdersAsync(
        DateOnly? date,
        string? status,
        CancellationToken ct = default)
    {
        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        var filterDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = filterDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = filterDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        query = query.Where(o => o.CreatedAt >= dayStart && o.CreatedAt <= dayEnd);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<OrderDto>>.Ok(orders.Select(ToDto).ToList(), "Orders");
    }

    public async Task<ServiceResult<OrderDto>> GetOrderByIdAsync(long id, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return ServiceResult<OrderDto>.NotFound($"Order {id} not found.");

        return ServiceResult<OrderDto>.Ok(ToDto(order));
    }

    public async Task<ServiceResult<OrderDto>> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<OrderDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<OrderDto>.BadRequest("Tenant context is required.");

        var total = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var order = new Order
        {
            TenantId    = tenantId,
            OrderType   = request.OrderType,
            TableNumber = request.TableNumber,
            Status      = OrderStatus.New,
            Total       = total,
            Notes       = request.Notes,
        };

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                TenantId  = tenantId,
                Name      = item.Name,
                Quantity  = item.Quantity,
                UnitPrice = item.UnitPrice,
                Notes     = item.Notes,
            });
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        orderMetrics.IncrementOrdersCreated();

        var dto = ToDto(order);
        var message = ToOrderCreatedMessage(order);
        var published = false;
        try
        {
            published = await messagePublisher.TryPublishAsync(
                message,
                MessageRouting.OrderCreatedRoutingKey,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "RabbitMQ publish failed, falling back to in-process broadcast: MessageId={MessageId} OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId}",
                message.MessageId, order.Id, tenantId, currentUserService.UserId);
        }

        if (!published)
        {
            await MarkOrderCreatedMessageProcessedAsync(message, ct);
            await notificationService.BroadcastOrderCreatedAsync(tenantId, dto, ct);

            logger.LogInformation(
                "Order created event broadcast via fallback: MessageId={MessageId} OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId}",
                message.MessageId, order.Id, tenantId, currentUserService.UserId);
        }

        logger.LogInformation(
            "Order created: OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId} Type={OrderType} Total={Total} ItemCount={ItemCount}",
            order.Id, tenantId, currentUserService.UserId, order.OrderType, order.Total, order.Items.Count);

        return ServiceResult<OrderDto>.Created(dto, "Order created.");
    }

    public async Task<ServiceResult<OrderDto>> UpdateStatusAsync(
        long id,
        UpdateOrderStatusRequest request,
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

        var oldStatus = order.Status.ToString();
        order.Status = Enum.Parse<OrderStatus>(request.Status);
        await db.SaveChangesAsync(ct);

        await notificationService.BroadcastOrderStatusChangedAsync(
            order.TenantId, order.Id, oldStatus, request.Status, ct);

        logger.LogInformation(
            "Order status changed: OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId} Previous={Previous} Current={Current}",
            order.Id, order.TenantId, currentUserService.UserId, oldStatus, request.Status);

        return ServiceResult<OrderDto>.Ok(ToDto(order), $"Order {id} status updated to {request.Status}.");
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
        UpdatedAt   = o.UpdatedAt ?? o.CreatedAt,
        Items       = o.Items.Select(i => new OrderItemDto
        {
            Id        = i.Id,
            Name      = i.Name,
            Quantity  = i.Quantity,
            UnitPrice = i.UnitPrice,
            Notes     = i.Notes,
        }).ToList()
    };

    private static OrderCreatedMessage ToOrderCreatedMessage(Order order) => new(
        MessageId: $"order-created-{order.Id}",
        OrderId: order.Id,
        TenantId: order.TenantId,
        OrderType: order.OrderType,
        TableNumber: order.TableNumber,
        Status: order.Status.ToString(),
        Total: order.Total,
        Notes: order.Notes,
        CreatedAt: order.CreatedAt,
        OccurredAt: DateTime.UtcNow,
        Items: order.Items.Select(i => new OrderItemMessage(
            i.Id,
            i.Name,
            i.Quantity,
            i.UnitPrice,
            i.Notes)).ToList());

    private async Task MarkOrderCreatedMessageProcessedAsync(
        OrderCreatedMessage message,
        CancellationToken ct)
    {
        if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == message.MessageId, ct))
            return;

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = message.MessageId,
            MessageType = nameof(OrderCreatedMessage),
            TenantId = message.TenantId,
            ProcessedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation(
                ex,
                "Fallback processed-message insert skipped after conflict: MessageId={MessageId} EventType={EventType} OrderId={OrderId} TenantId={TenantId}",
                message.MessageId, nameof(OrderCreatedMessage), message.OrderId, message.TenantId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
