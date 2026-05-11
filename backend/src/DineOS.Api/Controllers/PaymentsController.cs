using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Payment processing endpoints — Cashier and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Lists all open orders (not yet paid or cancelled) for the current tenant.</summary>
    [HttpGet("open-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOpenOrders(CancellationToken ct) =>
        (await paymentService.GetOpenOrdersAsync(ct)).ToActionResult();

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
        CancellationToken ct) =>
        (await paymentService.ProcessPaymentAsync(request, ct)).ToActionResult();
}
