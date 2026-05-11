using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Payments;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class PaymentService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<ProcessPaymentRequest> validator,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<ServiceResult<List<OrderDto>>> GetOpenOrdersAsync(CancellationToken ct = default)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
            .Select(o => new OrderDto
            {
                Id          = o.Id,
                OrderType   = o.OrderType,
                TableNumber = o.TableNumber,
                Status      = o.Status.ToString(),
                Total       = o.Total,
                Notes       = o.Notes,
                TenantId    = o.TenantId,
                CreatedAt   = o.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<OrderDto>>.Ok(orders, "Open orders");
    }

    public async Task<ServiceResult<PaymentDto>> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<PaymentDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<PaymentDto>.BadRequest("Tenant context is required.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null)
            return ServiceResult<PaymentDto>.NotFound($"Order {request.OrderId} not found.");

        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            return ServiceResult<PaymentDto>.UnprocessableEntity(
                $"Order {request.OrderId} is already {order.Status.ToString().ToLower()} and cannot be paid.");

        if (request.Amount != order.Total)
            return ServiceResult<PaymentDto>.UnprocessableEntity(
                $"Payment amount {request.Amount} does not match order total {order.Total}.");

        if (!Enum.TryParse<PaymentMethod>(request.Method, out var method))
            return ServiceResult<PaymentDto>.BadRequest("Invalid payment method.");

        var payment = new Payment
        {
            OrderId  = order.Id,
            TenantId = tenantId,
            Amount   = request.Amount,
            Method   = method,
            Status   = PaymentStatus.Completed
        };

        db.Payments.Add(payment);
        order.Status = OrderStatus.Delivered;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Payment processed: PaymentId={PaymentId} OrderId={OrderId} TenantId={TenantId} ActorUserId={ActorUserId} Amount={Amount} Method={Method}",
            payment.Id, order.Id, tenantId, currentUserService.UserId, payment.Amount, payment.Method);

        return ServiceResult<PaymentDto>.Created(ToDto(payment), "Payment processed.");
    }

    private static PaymentDto ToDto(Payment p) => new()
    {
        Id        = p.Id,
        OrderId   = p.OrderId,
        Amount    = p.Amount,
        Method    = p.Method.ToString(),
        Status    = p.Status.ToString(),
        TenantId  = p.TenantId,
        CreatedAt = p.CreatedAt
    };
}
