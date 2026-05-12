using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>
/// POS payment endpoints — Cashier, Manager, or SuperAdmin.
/// </summary>
/// <remarks>
/// All payment endpoints are tenant-scoped: they read the tenant from the
/// caller's JWT <c>tenant_id</c> claim and never trust the request body or the
/// <c>X-Tenant-ID</c> header alone. Cross-tenant reads or writes are not
/// possible through this controller.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[Authorize(Policy = "CashierAndAbove")]
[EnableRateLimiting("authenticated")]
public class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Lists open orders (not yet paid or cancelled) for the current tenant.</summary>
    /// <remarks>
    /// "Open" means the order's status is neither <c>Delivered</c> nor
    /// <c>Cancelled</c>. The returned list is the working set a cashier can
    /// settle through <c>POST /api/v1/payments</c>.
    /// </remarks>
    /// <param name="ct">Propagated cancellation token.</param>
    /// <response code="200">Open orders, possibly empty.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">Caller is authenticated but lacks Cashier/Manager/SuperAdmin role.</response>
    /// <response code="429">Rate limit exceeded for authenticated callers.</response>
    [HttpGet("open-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOpenOrders(CancellationToken ct) =>
        (await paymentService.GetOpenOrdersAsync(ct)).ToActionResult();

    /// <summary>Processes a payment for an open order and marks the order as <c>Delivered</c>.</summary>
    /// <remarks>
    /// Business rules enforced by the service:
    ///
    /// - The order must exist within the caller's tenant (<c>404</c> otherwise).
    /// - The order must not already be <c>Delivered</c> or <c>Cancelled</c>
    ///   (<c>422</c> otherwise).
    /// - <c>amount</c> must equal the order total exactly. Partial or over-
    ///   payments are rejected (<c>422</c>).
    /// - <c>method</c> must be one of <c>Cash</c> or <c>Card</c>.
    ///
    /// On success the order's status transitions to <c>Delivered</c> in the
    /// same database transaction as the payment insert.
    ///
    /// <c>POST</c> is **not** idempotent — replaying the same body after a
    /// successful 201 will return 422 because the order is no longer payable.
    /// </remarks>
    /// <param name="request">Payment payload (order id, exact amount, method).</param>
    /// <param name="ct">Propagated cancellation token.</param>
    /// <response code="201">Payment recorded; the response body carries the new <c>PaymentDto</c>.</response>
    /// <response code="400">Request failed FluentValidation (e.g. missing fields, negative amount).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">Caller is authenticated but lacks Cashier/Manager/SuperAdmin role, or tenant mismatch.</response>
    /// <response code="404">Order does not exist in the caller's tenant.</response>
    /// <response code="422">Order is already settled/cancelled, or <c>amount</c> does not match the order total.</response>
    /// <response code="429">Rate limit exceeded for authenticated callers.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken ct) =>
        (await paymentService.ProcessPaymentAsync(request, ct)).ToActionResult();
}
