using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Signup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/signup")]
[Produces("application/json")]
public class SignupController(ISignupService signupService) : ControllerBase
{
    private const string BillingUnavailableMessage = "Billing provider is unavailable. Please try again later.";

    /// <summary>
    /// Public restaurant signup. Creates a pending tenant and returns a Stripe
    /// Checkout URL the caller will redirect the user to. Tenant is provisioned
    /// after the webhook confirms payment (handled by BillingService).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<SignupResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StartSignup(
        [FromBody] SignupRequest request,
        CancellationToken ct)
    {
        var result = await signupService.StartSignupAsync(request, ct);
        return MapResult(result);
    }

    /// <summary>
    /// Returns the public-signup payment status for a Stripe Checkout session.
    /// Frontend polls this from the success page until the webhook flips the
    /// tenant to Active.
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<SignupStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] string sessionId,
        CancellationToken ct)
    {
        var result = await signupService.GetStatusAsync(sessionId, ct);
        return MapResult(result);
    }

    /// <summary>
    /// Sets the tenant owner's Keycloak password using the single-use token
    /// emailed after a successful Stripe Checkout. The dineOS frontend's
    /// <c>/set-password</c> page POSTs here so the owner never sees the
    /// Keycloak account console.
    /// </summary>
    [HttpPost("set-password")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CompleteSetup(
        [FromBody] SetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await signupService.CompleteSetupAsync(request, ct);
        return result.ToActionResult();
    }

    private IActionResult MapResult<T>(ServiceResult<T> result)
    {
        // Map "Stripe unavailable" UnprocessableEntity → 503 to match the
        // documented contract (issue #204). Everything else flows through the
        // standard extension.
        if (!result.IsSuccess &&
            result.Error == ServiceErrorKind.UnprocessableEntity &&
            string.Equals(result.Message, BillingUnavailableMessage, StringComparison.Ordinal))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail(BillingUnavailableMessage));
        }

        return result.ToActionResult();
    }
}
