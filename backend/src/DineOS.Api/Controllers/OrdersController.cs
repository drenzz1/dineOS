using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Orders;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>Order management endpoints — Cashier and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class OrdersController(
    AppDbContext db,
    ITenantService tenantService,
    IValidator<CreateOrderRequest> createValidator,
    IValidator<UpdateOrderStatusRequest> statusValidator) : ControllerBase
{
    /// <summary>Lists orders for the current tenant, with optional date and status filters.</summary>
    /// <param name="date">Filter by creation date (ISO format: yyyy-MM-dd). Defaults to today when omitted.</param>
    /// <param name="status">Filter by order status (New, InProgress, Ready, Delivered, Cancelled).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] DateOnly? date,
        [FromQuery] string? status,
        CancellationToken ct)
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

        return Ok(ApiResponse<List<OrderDto>>.Ok(orders.Select(ToDto).ToList(), "Orders"));
    }

    /// <summary>Gets a single order by ID, including all items.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrder(long id, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ApiResponse.Fail($"Order {id} not found."));

        return Ok(ApiResponse<OrderDto>.Ok(ToDto(order)));
    }

    /// <summary>Creates a new order and its line items. Total is computed from items.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

        var total = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var order = new Order
        {
            TenantId  = tenantId,
            OrderType = request.OrderType,
            TableNumber = request.TableNumber,
            Status    = OrderStatus.New,
            Total     = total,
            Notes     = request.Notes,
        };

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                TenantId   = tenantId,
                Name       = item.Name,
                Quantity   = item.Quantity,
                UnitPrice  = item.UnitPrice,
                Notes      = item.Notes,
            });
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<OrderDto>.Ok(ToDto(order), "Order created."));
    }

    /// <summary>Updates the status of an order.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateStatus(
        long id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var validation = await statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ApiResponse.Fail($"Order {id} not found."));

        order.Status = Enum.Parse<OrderStatus>(request.Status);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<OrderDto>.Ok(ToDto(order),
            $"Order {id} status updated to {request.Status}."));
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
