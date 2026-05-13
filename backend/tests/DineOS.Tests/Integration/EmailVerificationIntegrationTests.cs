using DineOS.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// Auth-gate tests for /api/v1/admin/restaurants/{tenantId}/email-verification.
///
/// Resend stays SuperAdmin-only (operator action). Confirm is owner-facing —
/// the 6-digit code is itself proof of inbox ownership, so the action is
/// AllowAnonymous and protected by the per-IP "email-verification-confirm"
/// rate limit plus the in-row FailedAttempts cap. See issue #173.
/// </summary>
[Collection("IntegrationTests")]
public class EmailVerificationIntegrationTests(CustomWebApplicationFactory factory)
{
    private const long SeededTenantId = 1L;

    // ── Confirm: owner-facing, AllowAnonymous ─────────────────────────────

    [Fact]
    public async Task Confirm_Anonymous_DoesNotReturn401Or403()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/admin/restaurants/{SeededTenantId}/email-verification/confirm",
            JsonContent(new { code = "000000" }));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_Anonymous_UnknownTenant_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/admin/restaurants/99999/email-verification/confirm",
            JsonContent(new { code = "000000" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_SuperAdmin_StillReachesService()
    {
        var client = ClientWith(GenerateJwt("SuperAdmin"));

        var response = await client.PostAsync(
            $"/api/v1/admin/restaurants/{SeededTenantId}/email-verification/confirm",
            JsonContent(new { code = "000000" }));

        // Either 200 (already verified) or 400 (invalid code) — both prove the
        // SuperAdmin path still reaches the service, and neither is a regression.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Resend: still SuperAdmin-only ─────────────────────────────────────

    [Fact]
    public async Task Resend_Anonymous_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/admin/restaurants/{SeededTenantId}/email-verification/resend",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resend_NonSuperAdmin_Returns403()
    {
        var client = ClientWith(GenerateJwt("Manager", SeededTenantId.ToString()));

        var response = await client.PostAsync(
            $"/api/v1/admin/restaurants/{SeededTenantId}/email-verification/resend",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resend_SuperAdmin_Returns202()
    {
        var client = ClientWith(GenerateJwt("SuperAdmin"));

        var response = await client.PostAsync(
            $"/api/v1/admin/restaurants/{SeededTenantId}/email-verification/resend",
            content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient ClientWith(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static string GenerateJwt(string role, string? tenantId = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        var claims = new List<Claim>
        {
            new("sub",   $"test-{role.ToLower()}"),
            new("email", $"{role.ToLower()}@dineos.dev"),
            new("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
        };

        if (tenantId is not null)
            claims.Add(new Claim("tenant_id", tenantId));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
}
