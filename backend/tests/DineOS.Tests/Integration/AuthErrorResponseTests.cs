using DineOS.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

[Collection("IntegrationTests")]
public class AuthErrorResponseTests(CustomWebApplicationFactory factory)
{
    // ── Test 1: Missing token → 401 with structured JSON ──────────────────
    [Fact]
    public async Task GetProtectedEndpoint_NoToken_Returns401_WithStructuredJson()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(401, json.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", json.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("timestamp").GetString()));
    }

    // ── Test 2: Wrong role → 403 with structured JSON ─────────────────────
    [Fact]
    public async Task GetSuperAdminEndpoint_ManagerRole_Returns403_WithStructuredJson()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtWithRole("Manager"));

        // AdminController requires "SuperAdminOnly" policy
        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(403, json.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", json.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("timestamp").GetString()));
    }

    // ── Test 3: correlationId is distinct per request ─────────────────────
    [Fact]
    public async Task TwoForbiddenRequests_HaveDifferentCorrelationIds()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwtWithRole("Cashier"));

        var r1 = JsonDocument.Parse(
            await (await client.GetAsync("/api/v1/admin/users")).Content.ReadAsStringAsync()
        ).RootElement;

        var r2 = JsonDocument.Parse(
            await (await client.GetAsync("/api/v1/admin/users")).Content.ReadAsStringAsync()
        ).RootElement;

        var id1 = r1.GetProperty("correlationId").GetString();
        var id2 = r2.GetProperty("correlationId").GetString();

        Assert.NotEqual(id1, id2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static string GenerateJwtWithRole(string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        // realm_access lets KeycloakRolesTransformation extract the role.
        // Authorization fails at policy check, before TenantIsolationMiddleware runs.
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub", "test-user"),
                new Claim("email", "test@dineos.dev"),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
