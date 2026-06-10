using Asp.Versioning;
using DineOS.Application.Authorization;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
public class AuthController(
    IKeycloakAuthService authService,
    IStaffSessionService staffSessionService) : ControllerBase
{
    /// <summary>Authenticates a user through Keycloak and returns an access/refresh token pair.</summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(
            result.Value!,
            "Login successful."));
    }

    /// <summary>
    /// Rotates the temporary password issued to a freshly-provisioned tenant owner
    /// (#205) and returns a fresh token pair. Anonymous because the owner cannot
    /// complete the standard login flow until <c>UPDATE_PASSWORD</c> is cleared.
    /// </summary>
    [HttpPost("auth/first-login-password-change")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> FirstLoginPasswordChange(
        [FromBody] FirstLoginPasswordChangeRequest request,
        CancellationToken ct)
    {
        var result = await authService.ChangeFirstLoginPasswordAsync(request, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(
            result.Value!,
            "Password updated. You are now signed in."));
    }

    /// <summary>Exchanges a valid Keycloak refresh token for a new token pair and blacklists the old one.</summary>
    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(
            result.Value!,
            "Token refreshed successfully."));
    }

    /// <summary>
    /// Verifies a staff member's PIN within the authenticated business (tenant)
    /// and returns a short-lived, role-scoped staff-session token. Requires a
    /// Keycloak business token (the StaffSession scheme cannot bootstrap itself).
    /// </summary>
    [HttpPost("auth/staff-session")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableRateLimiting("staff-pin")]
    [ProducesResponseType(typeof(ApiResponse<StaffSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StartStaffSession(
        [FromBody] StartStaffSessionRequest request,
        CancellationToken ct)
    {
        var result = await staffSessionService.StartAsync(request, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return Ok(ApiResponse<StaffSessionResponse>.Ok(
            result.Value!,
            "Staff session started."));
    }

    /// <summary>
    /// Exchanges a staff refresh token for a fresh access token without
    /// re-entering the PIN. Anonymous — authenticated by the refresh token in
    /// the body.
    /// </summary>
    [HttpPost("auth/staff-session/refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<StaffSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshStaffSession(
        [FromBody] RefreshStaffSessionRequest request,
        CancellationToken ct)
    {
        var result = await staffSessionService.RefreshAsync(request.RefreshToken, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return Ok(ApiResponse<StaffSessionResponse>.Ok(result.Value!, "Staff session refreshed."));
    }

    /// <summary>
    /// Ends the current staff session: revokes the presented access token and
    /// the supplied refresh token. Idempotent — always 204.
    /// </summary>
    [HttpPost("auth/staff-session/end")]
    [Authorize(AuthenticationSchemes = AuthSchemes.StaffSession)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EndStaffSession(
        [FromBody] EndStaffSessionRequest request,
        CancellationToken ct)
    {
        var accessJti = User.FindFirstValue("jti");
        long? accessExp = long.TryParse(User.FindFirstValue("exp"), out var exp) ? exp : null;

        await staffSessionService.EndAsync(accessJti, accessExp, request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Changes the authenticated user's password. Verifies the current password then resets via the Keycloak Admin API.</summary>
    [HttpPost("auth/change-password")]
    [Authorize(Policy = Policies.BusinessAccountOnly)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var email = User.FindFirstValue("email") ?? User.FindFirstValue("preferred_username");
        var result = await authService.ChangePasswordAsync(email ?? string.Empty, request, ct);
        if (!result.IsSuccess)
            return ToFailureResponse(result.Error, result.Errors);

        return NoContent();
    }

    /// <summary>Blacklists the provided refresh token, effectively invalidating the session. Idempotent — returns 204 even if the token is already blacklisted or jti is missing.</summary>
    [HttpPost("auth/logout")]
    [Authorize(Policy = Policies.BusinessAccountOnly)]
    [EnableRateLimiting("authenticated")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var result = await authService.LogoutAsync(request, ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse.Fail(result.Error ?? "Logout failed.", result.Errors));

        return NoContent();
    }

    private ObjectResult ToFailureResponse(string? error, IReadOnlyList<string>? errors = null)
    {
        var message = error ?? "Authentication failed.";

        return message switch
        {
            "Validation failed." =>
                BadRequest(ApiResponse.Fail(message, errors)),
            "Identity provider is unavailable." =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail(message)),
            "Invalid response from identity provider." =>
                StatusCode(StatusCodes.Status502BadGateway, ApiResponse.Fail(message)),
            // 409 lets the FE distinguish "needs first-login password change"
            // from generic 401 invalid-credentials. Surfaces as a specific
            // error so the FE can route the user to /first-login.
            "Account requires first-login password change." =>
                Conflict(ApiResponse.Fail(message)),
            "Tenant context is required." =>
                BadRequest(ApiResponse.Fail(message)),
            "Staff sessions are not configured." =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail(message)),
            _ => Unauthorized(ApiResponse.Fail(message))
        };
    }
}
