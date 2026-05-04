using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

/// <summary>Order creation and payment endpoints — Cashier and above.</summary>
[ApiController]
[Route("api/v1/orders")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
public class OrdersController : ControllerBase
{
    /// <summary>Lists all orders.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetOrders() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Order list"));

    /// <summary>Gets a single order by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetOrder(Guid id) =>
        Ok(ApiResponse<object>.Ok(new { Id = id }, "Order details"));

    /// <summary>Creates a new order.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CreateOrder() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Order created"));

    /// <summary>Updates an existing order.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateOrder(Guid id) =>
        Ok(ApiResponse.Ok($"Order {id} updated"));

    /// <summary>Processes payment for an order.</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ProcessPayment(Guid id) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok($"Payment for order {id} processed"));
}
