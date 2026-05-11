using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Order management endpoints — Cashier and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class OrdersController(IOrderService orderService) : ControllerBase
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
        CancellationToken ct) =>
        (await orderService.GetOrdersAsync(date, status, ct)).ToActionResult();

    /// <summary>Gets a single order by ID, including all items.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrder(long id, CancellationToken ct) =>
        (await orderService.GetOrderByIdAsync(id, ct)).ToActionResult();

    /// <summary>Creates a new order and its line items. Total is computed from items.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct) =>
        (await orderService.CreateOrderAsync(request, ct)).ToActionResult();

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
        CancellationToken ct) =>
        (await orderService.UpdateStatusAsync(id, request, ct)).ToActionResult();
}
