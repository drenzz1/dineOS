using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DineOS.Application.Common;
using DineOS.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace DineOS.Tests.Integration;

public class ApiIntegrationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Test 1 ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetHealth_Returns200_WithSuccessEnvelope()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, result.Data.ValueKind);
    }

    // ── Test 2 ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task PostValidate_MissingName_Returns400_WithApiResponseFail()
    {
        var client = factory.CreateClient();
        var body = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/test/validate", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(json, JsonOpts);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Name is required", result.Message);
    }

    // ── Test 3a ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetSecure_NoToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/test/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Test 3b ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetSecure_ValidBearerToken_Returns200()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt());

        var response = await client.GetAsync("/test/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Test 4a ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetMe_NoToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Test 4b ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetMe_ValidBearerToken_Returns200WithUserInfo()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt("user-123", "test@dineos.dev"));

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(JsonValueKind.Object, result.Data.ValueKind);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a signed JWT for integration tests.
    /// - realm_access: parsed by KeycloakRolesTransformation into ClaimTypes.Role.
    /// - tenant_id: required by TenantIsolationMiddleware for non-SuperAdmin users.
    /// </summary>
    private static string GenerateTestJwt(
        string userId   = "test-user",
        string email    = "test@dineos.dev",
        string role     = "Manager",
        string tenantId = "1")
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub",                userId),
                new Claim("email",              email),
                new Claim("preferred_username", email),
                new Claim(ClaimTypes.Name,      email),
                new Claim("tenant_id",          tenantId),
                new Claim("realm_access",       JsonSerializer.Serialize(new { roles = new[] { role } }))
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
