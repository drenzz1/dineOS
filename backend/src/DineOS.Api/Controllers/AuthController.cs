using Asp.Versioning;
using DineOS.Application.Authorization;
using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Jobs;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
public class AuthController(
    IKeycloakAuthService authService,
    IStaffSessionService staffSessionService,
    IBackgroundJobClient backgroundJobs,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IOptions<KeycloakOptions> keycloakOptions) : ControllerBase
{
    private const string GoogleStateCookie = "dineos_google_oauth_state";
    private const string GoogleFromCookie = "dineos_google_oauth_from";
    private static readonly TimeSpan GoogleStateLifetime = TimeSpan.FromMinutes(5);
    private readonly KeycloakOptions _keycloakOptions = keycloakOptions.Value;

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

    /// <summary>Starts Google sign-in through the configured Keycloak identity provider.</summary>
    [HttpGet("auth/google")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin([FromQuery] string? from = null)
    {
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Response.Cookies.Append(
            GoogleStateCookie,
            state,
            GoogleStateCookieOptions(GoogleStateLifetime));

        if (IsSafeInternalPath(from))
        {
            Response.Cookies.Append(
                GoogleFromCookie,
                from!,
                GoogleStateCookieOptions(GoogleStateLifetime));
        }
        else
        {
            Response.Cookies.Delete(
                GoogleFromCookie,
                GoogleStateCookieOptions(TimeSpan.Zero));
        }

        return Redirect(authService.BuildGoogleAuthorizationUrl(state));
    }

    /// <summary>Completes the Keycloak Google broker flow and establishes the browser session.</summary>
    [HttpGet("auth/google/callback")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        Request.Cookies.TryGetValue(GoogleStateCookie, out var expectedState);
        Request.Cookies.TryGetValue(GoogleFromCookie, out var from);
        DeleteGoogleFlowCookies();

        if (!StateMatches(expectedState, state))
            return RedirectToFrontendCallback("invalid_oauth_state", null);

        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
            return RedirectToFrontendCallback("google_auth_failed", null);

        var result = await authService.ExchangeGoogleAuthorizationCodeAsync(code, ct);
        if (!result.IsSuccess || result.Value is null)
            return RedirectToFrontendCallback("google_token_exchange_failed", null);

        var tokens = result.Value;
        var sessionLifetime = tokens.RefreshExpiresIn ?? tokens.ExpiresIn;

        AppendBrowserCookie("access_token", tokens.AccessToken, sessionLifetime);
        AppendBrowserCookie("refresh_token", tokens.RefreshToken, tokens.RefreshExpiresIn);
        AppendBrowserCookie("business_token", tokens.AccessToken, sessionLifetime);
        AppendBrowserCookie("session_mode", "owner", sessionLifetime);

        return RedirectToFrontendCallback(null, IsSafeInternalPath(from) ? from : null);
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

    /// <summary>
    /// Requests a password-reset code for the given email (forgot password).
    /// Always returns the same 200 response whether or not an account exists —
    /// the Keycloak lookup happens inside the background job, so the response
    /// cannot be used to enumerate accounts.
    /// </summary>
    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        var validation = await forgotPasswordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail(
                "Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        backgroundJobs.Enqueue<PasswordResetEmailJob>(
            job => job.SendAsync(request.Email.Trim(), CancellationToken.None));

        return Ok(ApiResponse.Ok(
            "If an account exists for that email, a password reset code has been sent."));
    }

    /// <summary>Resets a forgotten password using the emailed one-time code. Anonymous: the code itself is proof of inbox ownership.</summary>
    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await authService.ResetForgottenPasswordAsync(request, ct);
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
            // Forgot-password redemption failures are client errors (bad/expired
            // code), not authentication failures — 400, not 401.
            "Reset code is invalid or expired. Request a new code and try again." =>
                BadRequest(ApiResponse.Fail(message)),
            "Staff sessions are not configured." =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail(message)),
            _ => Unauthorized(ApiResponse.Fail(message))
        };
    }

    private CookieOptions GoogleStateCookieOptions(TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/api/v1/auth/google/callback",
        MaxAge = maxAge
    };

    private void DeleteGoogleFlowCookies()
    {
        var options = GoogleStateCookieOptions(TimeSpan.Zero);
        Response.Cookies.Delete(GoogleStateCookie, options);
        Response.Cookies.Delete(GoogleFromCookie, options);
    }

    private void AppendBrowserCookie(string name, string value, int? maxAgeSeconds)
    {
        Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = maxAgeSeconds is > 0
                ? TimeSpan.FromSeconds(maxAgeSeconds.Value)
                : null
        });
    }

    private IActionResult RedirectToFrontendCallback(string? error, string? from)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(error))
            query.Add($"error={Uri.EscapeDataString(error)}");
        if (IsSafeInternalPath(from))
            query.Add($"from={Uri.EscapeDataString(from!)}");

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        return Redirect($"{_keycloakOptions.GetFrontendUrl()}/auth/callback{suffix}");
    }

    private static bool StateMatches(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }

    private static bool IsSafeInternalPath(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith('/')
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.StartsWith("/\\", StringComparison.Ordinal);
}
