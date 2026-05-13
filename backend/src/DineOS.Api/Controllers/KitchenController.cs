using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Kitchen;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Kitchen order workflow endpoints — KitchenStaff only.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kitchen")]
[Produces("application/json")]
[Authorize(Policy = Policies.KitchenStaffOnly)]
[EnableRateLimiting("authenticated")]
public class KitchenController(IKitchenService kitchenService) : ControllerBase
{
    /// <summary>Lists active kitchen orders (New, InProgress, Ready) for the current tenant.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetKitchenOrders(CancellationToken ct) =>
        (await kitchenService.GetKitchenOrdersAsync(ct)).ToActionResult();

    /// <summary>Updates the preparation status of a kitchen order.</summary>
    [HttpPut("orders/{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateOrderStatus(
        long id,
        [FromBody] UpdateKitchenOrderStatusRequest request,
        CancellationToken ct) =>
        (await kitchenService.UpdateOrderStatusAsync(id, request, ct)).ToActionResult();

    /// <summary>Returns counts of active kitchen orders by status.</summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(ApiResponse<KitchenQueueSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetQueue(CancellationToken ct) =>
        (await kitchenService.GetQueueSummaryAsync(ct)).ToActionResult();
}
