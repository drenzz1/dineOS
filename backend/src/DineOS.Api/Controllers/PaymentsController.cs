using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Payments;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>Payment processing endpoints — Cashier and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class PaymentsController(
    AppDbContext db,
    ITenantService tenantService,
    IValidator<ProcessPaymentRequest> validator) : ControllerBase
{
    /// <summary>Lists all open orders (not yet paid or cancelled) for the current tenant.</summary>
    [HttpGet("open-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOpenOrders(CancellationToken ct)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderType = o.OrderType,
                TableNumber = o.TableNumber,
                Status = o.Status.ToString(),
                Total = o.Total,
                Notes = o.Notes,
                TenantId = o.TenantId,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<OrderDto>>.Ok(orders, "Open orders"));
    }

    /// <summary>Processes a payment for an order and marks the order as delivered.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null)
            return NotFound(ApiResponse.Fail($"Order {request.OrderId} not found."));

        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            return UnprocessableEntity(ApiResponse.Fail(
                $"Order {request.OrderId} is already {order.Status.ToString().ToLower()} and cannot be paid."));

        if (request.Amount != order.Total)
            return UnprocessableEntity(ApiResponse.Fail(
                $"Payment amount {request.Amount} does not match order total {order.Total}."));

        if (!Enum.TryParse<PaymentMethod>(request.Method, out var method))
            return BadRequest(ApiResponse.Fail("Invalid payment method."));

        var payment = new Payment
        {
            OrderId = order.Id,
            TenantId = tenantId,
            Amount = request.Amount,
            Method = method,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);

        order.Status = OrderStatus.Delivered;

        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<PaymentDto>.Ok(ToDto(payment), "Payment processed."));
    }

    private static PaymentDto ToDto(Payment p) => new()
    {
        Id = p.Id,
        OrderId = p.OrderId,
        Amount = p.Amount,
        Method = p.Method.ToString(),
        Status = p.Status.ToString(),
        TenantId = p.TenantId,
        CreatedAt = p.CreatedAt
    };
}
