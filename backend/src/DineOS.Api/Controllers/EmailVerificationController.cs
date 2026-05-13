using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Restaurants;
using Hangfire;
using DineOS.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>
/// Restaurant-owner email verification — confirming a one-time code, or
/// re-issuing it via the Hangfire background job.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/restaurants/{tenantId:long}/email-verification")]
[Produces("application/json")]
[Authorize(Policy = "SuperAdminOnly")]
[EnableRateLimiting("authenticated")]
public class EmailVerificationController(
    IEmailVerificationService verificationService,
    IBackgroundJobClient backgroundJobs) : ControllerBase
{
    /// <summary>Re-enqueues the verification email for the given restaurant.</summary>
    [HttpPost("resend")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Resend(long tenantId)
    {
        var jobId = backgroundJobs.Enqueue<AccountVerificationEmailJob>(
            job => job.SendAsync(tenantId, CancellationToken.None));
        return Accepted(ApiResponse.Ok($"Verification email queued. JobId={jobId}"));
    }

    /// <summary>Confirms a verification code submitted by the owner.</summary>
    // Owner-facing: the 6-digit code is itself proof of inbox ownership, so we
    // override the class-level SuperAdminOnly policy. Brute-force is bounded by
    // the per-IP "email-verification-confirm" limiter plus the in-row
    // FailedAttempts cap on EmailVerificationCode.
    [HttpPost("confirm")]
    [AllowAnonymous]
    [EnableRateLimiting("email-verification-confirm")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Confirm(
        long tenantId,
        [FromBody] ConfirmEmailVerificationRequest request,
        CancellationToken ct) =>
        (await verificationService.ConfirmAccountVerificationCodeAsync(tenantId, request, ct))
            .ToActionResult();
}
