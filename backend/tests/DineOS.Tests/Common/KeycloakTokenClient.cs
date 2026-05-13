using System.Collections.Concurrent;
using System.Text.Json;
using DineOS.Tests.Fixtures;

namespace DineOS.Tests.Common;

/// <summary>
/// Issues and caches real Keycloak access tokens for seeded test users via the
/// Resource Owner Password Credentials grant on the dineos-frontend client.
///
/// Tokens are cached per username and reused until 30 seconds before their exp claim,
/// so repeated calls within a test run do not hammer the Testcontainer.
/// </summary>
public sealed class KeycloakTokenClient : IDisposable
{
    // dineos-frontend is the only public client with directAccessGrantsEnabled: true
    // (dineos-api is bearer-only with no direct-access grant — verified in realm-export.json).
    private const string ClientId = "dineos-frontend";

    // Seeded realm users (realm-export.json) — all share this password.
    private const string TestPassword = "Test1234!";

    private readonly HttpClient _http = new();
    private readonly string _tokenEndpoint;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();

    public KeycloakTokenClient(KeycloakContainerFixture fixture)
    {
        _tokenEndpoint = fixture.TokenEndpoint;
    }

    /// <summary>
    /// Returns a valid access token for <paramref name="username"/>.
    /// A cached token is returned if it has more than 30 seconds left before expiry.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(
        string username, string password, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(username, out var cached) &&
            cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.AccessToken;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"]  = ClientId,
            ["username"]   = username,
            ["password"]   = password,
        });

        var response = await _http.PostAsync(_tokenEndpoint, form, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var exp         = ExtractExp(accessToken);
        var expiresAt   = DateTimeOffset.FromUnixTimeSeconds(exp).AddSeconds(-30);

        _cache[username] = new CachedToken(accessToken, expiresAt);
        return accessToken;
    }

    // ── Convenience helpers (seeded users from realm-export.json) ─────────────────

    public Task<string> GetSuperAdminTokenAsync(CancellationToken ct = default) =>
        GetAccessTokenAsync("admin@dineos.dev", TestPassword, ct);

    public Task<string> GetManagerTokenAsync(CancellationToken ct = default) =>
        GetAccessTokenAsync("manager@dineos.dev", TestPassword, ct);

    public Task<string> GetCashierTokenAsync(CancellationToken ct = default) =>
        GetAccessTokenAsync("cashier@dineos.dev", TestPassword, ct);

    public Task<string> GetKitchenStaffTokenAsync(CancellationToken ct = default) =>
        GetAccessTokenAsync("kitchen@dineos.dev", TestPassword, ct);

    public void Dispose() => _http.Dispose();

    // ── JWT exp extraction ────────────────────────────────────────────────────────

    private static long ExtractExp(string jwt)
    {
        // JWT = <header>.<payload>.<signature>  — all parts are base64url-encoded.
        var payloadSegment = jwt.Split('.')[1];

        // base64url → standard base64
        var base64 = payloadSegment.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64,
        };

        var bytes = Convert.FromBase64String(base64);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.GetProperty("exp").GetInt64();
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}
