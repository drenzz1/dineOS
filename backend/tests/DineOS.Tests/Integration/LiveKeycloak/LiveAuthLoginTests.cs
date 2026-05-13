using DineOS.Tests.Common;
using DineOS.Tests.Fixtures;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration.LiveKeycloak;

[Collection("LiveAuth")]
[Trait(Traits.Category, Traits.LiveAuth)]
public class LiveAuthLoginTests : IAsyncLifetime
{
    private readonly KeycloakContainerFixture _keycloak;
    private readonly LiveKeycloakWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public LiveAuthLoginTests(KeycloakContainerFixture keycloak)
    {
        _keycloak = keycloak;
        _factory  = new LiveKeycloakWebApplicationFactory(keycloak);
    }

    public Task InitializeAsync() => ((IAsyncLifetime)_factory).InitializeAsync();
    public Task DisposeAsync()    => ((IAsyncLifetime)_factory).DisposeAsync();

    // ── a) Valid credentials → 200 + well-formed token pair ──────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessAndRefreshTokens()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/login",
            Json(new { username = "manager@dineos.dev", password = "Test1234!" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc   = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root         = doc.RootElement;
        var data         = root.GetProperty("data");

        var accessToken  = data.GetProperty("accessToken").GetString()!;
        var refreshToken = data.GetProperty("refreshToken").GetString()!;
        var expiresIn    = data.GetProperty("expiresIn").GetInt32();

        Assert.False(string.IsNullOrEmpty(accessToken),  "access_token must be non-empty");
        Assert.False(string.IsNullOrEmpty(refreshToken), "refresh_token must be non-empty");
        Assert.True(expiresIn > 0,                       "expires_in must be positive");

        // ── JWT claim assertions (read-only, no signature validation needed here) ─

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        // iss must match the Testcontainer realm URL
        Assert.Equal(_keycloak.Authority, jwt.Issuer);

        // dineos-api-audience protocol mapper adds "dineos-api" to aud
        Assert.Contains("dineos-api", jwt.Audiences);

        // Keycloak stores realm roles in realm_access.roles (JSON object claim)
        var realmAccessClaim = jwt.Claims.FirstOrDefault(c => c.Type == "realm_access");
        Assert.NotNull(realmAccessClaim);

        using var realmDoc = JsonDocument.Parse(realmAccessClaim.Value);
        var roles = realmDoc.RootElement
            .GetProperty("roles")
            .EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        Assert.Contains("Manager", roles);
    }

    // ── b) Wrong password → 401 ───────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/login",
            Json(new { username = "manager@dineos.dev", password = "WrongPassword!" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── c) No bearer token → 401 ─────────────────────────────────────────────────

    [Fact]
    public async Task ProtectedEndpoint_WithoutBearer_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/menu");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
