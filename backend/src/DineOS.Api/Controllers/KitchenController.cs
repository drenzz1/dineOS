using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

/// <summary>Kitchen order workflow endpoints — KitchenStaff only.</summary>
[ApiController]
[Route("api/v1/kitchen")]
[Produces("application/json")]
[Authorize(Policy = "KitchenStaffOnly")]
public class KitchenController : ControllerBase
{
    /// <summary>Lists all orders in the kitchen queue.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetKitchenOrders() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Kitchen order queue"));

    /// <summary>Updates the preparation status of a kitchen order.</summary>
    [HttpPut("orders/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateOrderStatus(Guid id) =>
        Ok(ApiResponse.Ok($"Order {id} status updated"));

    /// <summary>Returns the current kitchen queue summary.</summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetQueue() =>
        Ok(ApiResponse<object>.Ok(new { Pending = 0, InProgress = 0, Ready = 0 }, "Kitchen queue summary"));
}
