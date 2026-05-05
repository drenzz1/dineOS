using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DineOS.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class AuthController(
    ITokenBlacklistService tokenBlacklist,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Exchanges a valid Keycloak refresh token for a new token pair and blacklists the old one.</summary>
    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Decode the incoming token once to extract jti/exp — no signature validation needed here.
        var handler = new JwtSecurityTokenHandler();
        string? jti     = null;
        long    expUnix = 0;

        if (handler.CanReadToken(request.RefreshToken))
        {
            var parsed   = handler.ReadJwtToken(request.RefreshToken);
            jti          = parsed.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            var expClaim = parsed.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            long.TryParse(expClaim, out expUnix);
        }

        // Reject immediately if this jti is already blacklisted — token reuse detected.
        if (jti is not null && await tokenBlacklist.IsBlacklistedAsync(jti))
            return Unauthorized(ApiResponse.Fail("Refresh token has been revoked."));

        var authority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");

        var client = httpClientFactory.CreateClient();
        var keycloakResponse = await client.PostAsync(
            $"{authority}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["client_id"]     = "dineos-api",
                ["refresh_token"] = request.RefreshToken
            }));

        if (!keycloakResponse.IsSuccessStatusCode)
            return Unauthorized(ApiResponse.Fail("Invalid or expired refresh token."));

        var payload = await keycloakResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>();
        if (payload?.AccessToken is null || payload.RefreshToken is null)
            return Unauthorized(ApiResponse.Fail("Invalid response from identity provider."));

        // Blacklist the old jti now that Keycloak has issued a new pair.
        if (jti is not null)
        {
            var ttl = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
            if (ttl < TimeSpan.Zero) ttl = TimeSpan.Zero;
            await tokenBlacklist.BlacklistAsync(jti, ttl);
        }

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(
            new RefreshTokenResponse(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn),
            "Token refreshed successfully."));
    }

    /// <summary>Blacklists the provided refresh token, effectively invalidating the session. Idempotent — returns 204 even if the token is already blacklisted or jti is missing.</summary>
    [HttpPost("auth/logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var handler = new JwtSecurityTokenHandler();
        if (handler.CanReadToken(request.RefreshToken))
        {
            var token    = handler.ReadJwtToken(request.RefreshToken);
            var jti      = token.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            var expClaim = token.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;

            if (jti is not null && expClaim is not null && long.TryParse(expClaim, out var expUnix))
            {
                var ttl = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
                if (ttl < TimeSpan.Zero) ttl = TimeSpan.Zero;
                await tokenBlacklist.BlacklistAsync(jti, ttl);
            }
        }

        return NoContent();
    }

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")]  string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")]    int     ExpiresIn);
}
