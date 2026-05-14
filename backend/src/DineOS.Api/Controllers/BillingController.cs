using Asp.Versioning;
using DineOS.Application.Authorization;
using DineOS.Application.Billing;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>
/// SaaS subscription endpoints — manage the tenant's own dineOS subscription
/// (not in-restaurant payments). Backed by Stripe Billing.
/// </summary>
/// <remarks>
/// Three caller-facing endpoints require an authenticated Manager+ user;
/// <c>POST /webhook</c> is anonymous and authenticated via Stripe signature.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing")]
[Produces("application/json")]
public class BillingController(IBillingService billingService) : ControllerBase
{
    /// <summary>Returns the current tenant's subscription state.</summary>
    [HttpGet("subscription")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<BillingSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSubscription(CancellationToken ct) =>
        (await billingService.GetSubscriptionAsync(ct)).ToActionResult();

    /// <summary>Creates a Stripe Checkout session for the requested cycle and returns a URL the client should redirect to.</summary>
    [HttpPost("checkout-session")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<StripeRedirectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken ct) =>
        (await billingService.CreateCheckoutSessionAsync(request.Cycle, ct)).ToActionResult();

    /// <summary>Creates a Stripe Customer Portal session so the tenant can swap card, cancel, or view invoices.</summary>
    [HttpPost("portal-session")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(typeof(ApiResponse<StripeRedirectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePortalSession(CancellationToken ct) =>
        (await billingService.CreatePortalSessionAsync(ct)).ToActionResult();

    /// <summary>Stripe webhook target. Anonymous — authenticated via the <c>Stripe-Signature</c> header.</summary>
    /// <remarks>
    /// Stripe sends subscription lifecycle and invoice events here. The request
    /// body must be read raw (not as parsed JSON) so the signature can be
    /// verified byte-for-byte.
    /// </remarks>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        return (await billingService.HandleWebhookAsync(json, signature, ct)).ToActionResult();
    }
}
