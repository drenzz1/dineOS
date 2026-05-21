using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Authentication;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Auth;

/// <summary>
/// Authenticates to Keycloak as a confidential client (the
/// <c>dineos-admin</c> service account, <c>client_credentials</c> grant)
/// and exposes the slice of the Admin REST API used by the Stripe-driven
/// owner provisioning flow (#205). Token is cached in-memory and refreshed
/// ~30s before its <c>expires_in</c> elapses.
/// </summary>
public sealed class KeycloakAdminClient : IKeycloakAdminClient
{
    public const string HttpClientName = "keycloak-admin";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminClient> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public KeycloakAdminClient(
        IHttpClientFactory httpFactory,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string tempPassword,
        IReadOnlyList<string> requiredActions,
        CancellationToken ct)
    {
        var realm = RequireRealm();
        using var http = await CreateAuthenticatedClientAsync(ct);

        var payload = new
        {
            username = email,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = false,
            requiredActions,
            credentials = new[]
            {
                new { type = "password", value = tempPassword, temporary = true }
            }
        };

        using var response = await http.PostAsJsonAsync(
            $"admin/realms/{realm}/users", payload, JsonOptions, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInformation(
                "Keycloak reported user already exists; looking up by email {Email}.", email);
            return await FindUserIdByEmailAsync(http, realm, email, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)response.StatusCode, $"Keycloak user create failed: {body}");
        }

        var location = response.Headers.Location?.ToString()
            ?? throw new KeycloakAdminException(
                (int)response.StatusCode,
                "Keycloak user create succeeded but Location header was missing.");

        return location.TrimEnd('/').Split('/')[^1];
    }

    public async Task SetPasswordAsync(string userId, string newPassword, CancellationToken ct)
    {
        var realm = RequireRealm();
        using var http = await CreateAuthenticatedClientAsync(ct);

        var payload = new { type = "password", value = newPassword, temporary = false };

        using var response = await http.PutAsJsonAsync(
            $"admin/realms/{realm}/users/{userId}/reset-password", payload, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)response.StatusCode, $"Keycloak set password failed: {body}");
        }
    }

    public async Task ClearRequiredActionsAsync(
        string userId,
        IReadOnlyList<string> actionsToRemove,
        bool? emailVerified,
        CancellationToken ct)
    {
        var realm = RequireRealm();
        using var http = await CreateAuthenticatedClientAsync(ct);

        // Fetch current representation, filter requiredActions, PUT back.
        using var getResponse = await http.GetAsync($"admin/realms/{realm}/users/{userId}", ct);
        if (!getResponse.IsSuccessStatusCode)
        {
            var body = await getResponse.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)getResponse.StatusCode, $"Keycloak user lookup failed: {body}");
        }

        var user = await getResponse.Content.ReadFromJsonAsync<UserRepresentation>(JsonOptions, ct)
            ?? throw new KeycloakAdminException(500, "Keycloak user lookup returned empty body.");

        var remaining = (user.RequiredActions ?? Array.Empty<string>())
            .Where(a => !actionsToRemove.Contains(a, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        // Build update dict so emailVerified is only sent when caller wants
        // to change it — Keycloak's PUT merges supplied fields onto the user.
        var update = new Dictionary<string, object?>
        {
            ["requiredActions"] = remaining,
        };
        if (emailVerified is bool ev)
        {
            update["emailVerified"] = ev;
        }

        using var putResponse = await http.PutAsJsonAsync(
            $"admin/realms/{realm}/users/{userId}", update, JsonOptions, ct);

        if (!putResponse.IsSuccessStatusCode)
        {
            var body = await putResponse.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)putResponse.StatusCode, $"Keycloak clear required actions failed: {body}");
        }
    }

    public async Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken ct)
    {
        var realm = RequireRealm();
        using var http = await CreateAuthenticatedClientAsync(ct);

        var role = await GetOrCreateRealmRoleAsync(http, realm, roleName, ct);

        var assignment = new[] { new { id = role.Id, name = role.Name } };
        using var response = await http.PostAsJsonAsync(
            $"admin/realms/{realm}/users/{userId}/role-mappings/realm",
            assignment, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)response.StatusCode, $"Keycloak role assignment failed: {body}");
        }
    }

    private async Task<RealmRole> GetOrCreateRealmRoleAsync(
        HttpClient http, string realm, string roleName, CancellationToken ct)
    {
        var roleUrl = $"admin/realms/{realm}/roles/{Uri.EscapeDataString(roleName)}";

        using (var getResponse = await http.GetAsync(roleUrl, ct))
        {
            if (getResponse.IsSuccessStatusCode)
            {
                return await getResponse.Content.ReadFromJsonAsync<RealmRole>(JsonOptions, ct)
                    ?? throw new KeycloakAdminException(
                        500, $"Realm role '{roleName}' lookup returned empty body.");
            }

            if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await getResponse.Content.ReadAsStringAsync(ct);
                throw new KeycloakAdminException(
                    (int)getResponse.StatusCode, $"Realm role lookup failed: {body}");
            }
        }

        _logger.LogInformation(
            "Realm role '{Role}' missing from Keycloak — creating it idempotently.", roleName);

        using (var createResponse = await http.PostAsJsonAsync(
            $"admin/realms/{realm}/roles", new { name = roleName }, JsonOptions, ct))
        {
            if (!createResponse.IsSuccessStatusCode
                && createResponse.StatusCode != HttpStatusCode.Conflict)
            {
                var body = await createResponse.Content.ReadAsStringAsync(ct);
                throw new KeycloakAdminException(
                    (int)createResponse.StatusCode, $"Realm role create failed: {body}");
            }
        }

        using var refetch = await http.GetAsync(roleUrl, ct);
        if (!refetch.IsSuccessStatusCode)
        {
            var body = await refetch.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)refetch.StatusCode, $"Realm role re-lookup after create failed: {body}");
        }

        return await refetch.Content.ReadFromJsonAsync<RealmRole>(JsonOptions, ct)
            ?? throw new KeycloakAdminException(
                500, $"Realm role '{roleName}' re-lookup returned empty body.");
    }

    private static async Task<string> FindUserIdByEmailAsync(
        HttpClient http, string realm, string email, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            $"admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new KeycloakAdminException(
                (int)response.StatusCode, $"Keycloak user lookup by email failed: {body}");
        }

        var users = await response.Content.ReadFromJsonAsync<List<UserSummary>>(JsonOptions, ct);
        return users?.FirstOrDefault()?.Id
            ?? throw new KeycloakAdminException(
                404,
                $"Keycloak reported 409 on user create but no user with email '{email}' could be found.");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var http = _httpFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(_options.GetAdminBaseUrl().TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            var realm = RequireRealm();
            var clientId = _options.AdminClientId
                ?? throw new InvalidOperationException("Keycloak:AdminClientId is not configured.");
            var clientSecret = _options.AdminClientSecret
                ?? throw new InvalidOperationException("Keycloak:AdminClientSecret is not configured.");

            var tokenUrl = $"{_options.GetAdminBaseUrl().TrimEnd('/')}/realms/{realm}/protocol/openid-connect/token";
            using var http = _httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                })
            };

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new KeycloakAdminException(
                    (int)response.StatusCode,
                    $"Keycloak admin token endpoint returned {(int)response.StatusCode}: {body}");
            }

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct)
                ?? throw new KeycloakAdminException(
                    500, "Keycloak admin token endpoint returned an unparseable payload.");

            _cachedToken = payload.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, payload.ExpiresIn - 30));
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string RequireRealm() =>
        _options.Realm
        ?? throw new InvalidOperationException("Keycloak:Realm is not configured.");

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record RealmRole(string Id, string Name);

    private sealed record UserSummary(string Id);

    private sealed record UserRepresentation(
        string Id,
        [property: JsonPropertyName("requiredActions")] string[]? RequiredActions);
}
