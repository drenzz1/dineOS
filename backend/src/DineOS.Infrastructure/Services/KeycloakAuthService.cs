using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DineOS.Infrastructure.Services;

public sealed class KeycloakAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakOptions> options,
    ITokenBlacklistService tokenBlacklist,
    ILogger<KeycloakAuthService> logger) : IKeycloakAuthService
{
    public const string HttpClientName = "Keycloak";

    private readonly KeycloakOptions _options = options.Value;

    public async Task<Result<RefreshTokenResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Result<RefreshTokenResponse>.Failure("Username and password are required.");

        var form = CreateClientForm();
        form["grant_type"] = string.IsNullOrWhiteSpace(_options.GrantType) ? "password" : _options.GrantType;
        form["username"] = request.Username;
        form["password"] = request.Password;

        var result = await ExchangeTokenAsync(
            form,
            "Invalid username or password.",
            cancellationToken);

        if (result.IsSuccess)
            logger.LogInformation("User {Username} authenticated through Keycloak.", request.Username);
        else
            logger.LogWarning("Keycloak login failed for user {Username}: {Reason}", request.Username, result.Error);

        return result;
    }

    public async Task<Result<RefreshTokenResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<RefreshTokenResponse>.Failure("Refresh token is required.");

        var tokenInfo = DecodeRefreshToken(request.RefreshToken);

        if (tokenInfo.Jti is not null && await tokenBlacklist.IsBlacklistedAsync(tokenInfo.Jti))
        {
            logger.LogWarning("Rejected refresh token reuse for jti {Jti}.", tokenInfo.Jti);
            return Result<RefreshTokenResponse>.Failure("Refresh token has been revoked.");
        }

        var form = CreateClientForm();
        form["grant_type"] = "refresh_token";
        form["refresh_token"] = request.RefreshToken;

        var result = await ExchangeTokenAsync(
            form,
            "Invalid or expired refresh token.",
            cancellationToken);

        if (!result.IsSuccess)
            return result;

        if (tokenInfo.Jti is not null)
        {
            var ttl = CalculateRemainingTtl(tokenInfo.ExpiresAtUnix);
            await tokenBlacklist.BlacklistAsync(tokenInfo.Jti, ttl);
            logger.LogInformation("Blacklisted rotated refresh token jti {Jti}.", tokenInfo.Jti);
        }

        return result;
    }

    public async Task<Result> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Failure("Refresh token is required.");

        var tokenInfo = DecodeRefreshToken(request.RefreshToken);

        if (tokenInfo.Jti is not null)
        {
            var ttl = CalculateRemainingTtl(tokenInfo.ExpiresAtUnix);
            await tokenBlacklist.BlacklistAsync(tokenInfo.Jti, ttl);
            logger.LogInformation("Blacklisted logout refresh token jti {Jti}.", tokenInfo.Jti);
        }
        else
        {
            logger.LogDebug("Logout refresh token did not include a readable jti claim.");
        }

        await RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);

        return Result.Success();
    }

    private async Task<Result<RefreshTokenResponse>> ExchangeTokenAsync(
        Dictionary<string, string> form,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await CreateClient().PostAsync(GetTokenEndpoint(), content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Keycloak token endpoint returned {StatusCode} for grant_type {GrantType}.",
                    (int)response.StatusCode,
                    form.GetValueOrDefault("grant_type"));
                return Result<RefreshTokenResponse>.Failure(failureMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(
                cancellationToken: cancellationToken);

            if (payload?.AccessToken is null || payload.RefreshToken is null)
            {
                logger.LogError("Keycloak token endpoint returned an invalid token payload.");
                return Result<RefreshTokenResponse>.Failure("Invalid response from identity provider.");
            }

            return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(
                payload.AccessToken,
                payload.RefreshToken,
                payload.ExpiresIn,
                payload.RefreshExpiresIn));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Keycloak token endpoint is unavailable.");
            return Result<RefreshTokenResponse>.Failure("Identity provider is unavailable.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Keycloak token endpoint timed out.");
            return Result<RefreshTokenResponse>.Failure("Identity provider is unavailable.");
        }
    }

    private async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var form = CreateClientForm();
        form["token"] = refreshToken;
        form["token_type_hint"] = "refresh_token";

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await CreateClient().PostAsync(GetRevocationEndpoint(), content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Refresh token revoked through Keycloak.");
                return;
            }

            logger.LogWarning(
                "Keycloak revocation endpoint returned {StatusCode}. Local refresh-token blacklist is still applied.",
                (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Keycloak revocation endpoint is unavailable. Local refresh-token blacklist is still applied.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Keycloak revocation endpoint timed out. Local refresh-token blacklist is still applied.");
        }
    }

    private Dictionary<string, string> CreateClientForm()
    {
        var clientId = _options.GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Keycloak:ClientId is not configured.");

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        return form;
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

    private string GetTokenEndpoint() =>
        _options.GetBackchannelTokenEndpoint()
        ?? throw new InvalidOperationException("Keycloak token endpoint is not configured.");

    private string GetRevocationEndpoint() =>
        _options.GetBackchannelRevocationEndpoint()
        ?? throw new InvalidOperationException("Keycloak revocation endpoint is not configured.");

    private static RefreshTokenInfo DecodeRefreshToken(string refreshToken)
    {
        var parts = refreshToken.Split('.');
        if (parts.Length < 2)
            return new RefreshTokenInfo(null, null);

        try
        {
            var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var jti = root.TryGetProperty("jti", out var jtiElement)
                ? jtiElement.GetString()
                : null;

            long? exp = null;
            if (root.TryGetProperty("exp", out var expElement))
            {
                exp = expElement.ValueKind switch
                {
                    JsonValueKind.Number when expElement.TryGetInt64(out var value) => value,
                    JsonValueKind.String when long.TryParse(expElement.GetString(), out var value) => value,
                    _ => null
                };
            }

            return new RefreshTokenInfo(jti, exp);
        }
        catch (JsonException)
        {
            return new RefreshTokenInfo(null, null);
        }
        catch (FormatException)
        {
            return new RefreshTokenInfo(null, null);
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        output = output.PadRight(output.Length + (4 - output.Length % 4) % 4, '=');
        return Convert.FromBase64String(output);
    }

    private static TimeSpan CalculateRemainingTtl(long? expUnix)
    {
        if (expUnix is null)
            return TimeSpan.Zero;

        var ttl = DateTimeOffset.FromUnixTimeSeconds(expUnix.Value) - DateTimeOffset.UtcNow;
        return ttl < TimeSpan.Zero ? TimeSpan.Zero : ttl;
    }

    private sealed record RefreshTokenInfo(string? Jti, long? ExpiresAtUnix);

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_expires_in")] int? RefreshExpiresIn);
}
