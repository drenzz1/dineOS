using Asp.Versioning;
using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Order creation and payment endpoints — Cashier and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class OrdersController : ControllerBase
{
    /// <summary>Lists all orders. Supports cursor pagination via <c>cursor</c> and <c>pageSize</c> query params.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResponse<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetOrders([FromQuery] CursorPagedRequest request) =>
        Ok(ApiResponse<CursorPagedResponse<object>>.Ok(
            new CursorPagedResponse<object> { Items = [] }, "Order list"));

    /// <summary>Gets a single order by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetOrder(Guid id) =>
        Ok(ApiResponse<object>.Ok(new { Id = id }, "Order details"));

    /// <summary>Creates a new order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult CreateOrder() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Order created"));

    /// <summary>Updates an existing order.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult UpdateOrder(Guid id) =>
        Ok(ApiResponse.Ok($"Order {id} updated"));

    /// <summary>Processes payment for an order.</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult ProcessPayment(Guid id) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok($"Payment for order {id} processed"));
}
