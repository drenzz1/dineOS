using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
public class AuthController(IKeycloakAuthService authService) : ControllerBase
{
    /// <summary>Authenticates a user through Keycloak and returns an access/refresh token pair.</summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
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

    /// <summary>Blacklists the provided refresh token, effectively invalidating the session. Idempotent — returns 204 even if the token is already blacklisted or jti is missing.</summary>
    [HttpPost("auth/logout")]
    [Authorize]
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
            _ => Unauthorized(ApiResponse.Fail(message))
        };
    }
}
