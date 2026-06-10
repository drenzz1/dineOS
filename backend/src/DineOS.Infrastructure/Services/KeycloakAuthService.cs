using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using FluentValidation;
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
    IKeycloakAdminClient keycloakAdmin,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshTokenRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator,
    IValidator<FirstLoginPasswordChangeRequest> firstLoginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator,
    IEmailVerificationService emailVerification,
    ILogger<KeycloakAuthService> logger) : IKeycloakAuthService
{
    public const string HttpClientName = "Keycloak";
    private const string ValidationFailedMessage = "Validation failed.";
    private const string FirstLoginRequiredAction = "UPDATE_PASSWORD";

    private readonly KeycloakOptions _options = options.Value;

    public async Task<Result<RefreshTokenResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<RefreshTokenResponse>.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

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
        var validation = await refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<RefreshTokenResponse>.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

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

    public async Task<Result<RefreshTokenResponse>> ChangeFirstLoginPasswordAsync(
        FirstLoginPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await firstLoginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<RefreshTokenResponse>.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

        var user = await keycloakAdmin.FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // Do not leak whether the email exists — same response as a bad password.
            return Result<RefreshTokenResponse>.Failure("Invalid email or temporary password.");
        }

        if (!user.RequiredActions.Contains(FirstLoginRequiredAction))
        {
            // The owner has already rotated the password (or was never in the
            // first-login state). Reject so this endpoint cannot be used as a
            // general unauthenticated password-reset primitive.
            logger.LogWarning(
                "First-login password change rejected — user {Email} has no pending UPDATE_PASSWORD action.",
                request.Email);
            return Result<RefreshTokenResponse>.Failure(
                "This account is not in the first-login state. Use the standard login flow.");
        }

        // Clear the required action so the direct-grant verification below
        // can complete. If the temporary password turns out to be wrong we
        // restore the action before returning, so the account stays gated.
        await keycloakAdmin.SetRequiredActionsAsync(user.Id, Array.Empty<string>(), cancellationToken);

        var verifyForm = CreateClientForm();
        verifyForm["grant_type"] = string.IsNullOrWhiteSpace(_options.GrantType) ? "password" : _options.GrantType;
        verifyForm["username"] = request.Email;
        verifyForm["password"] = request.CurrentPassword;

        var verification = await ExchangeTokenAsync(
            verifyForm,
            "Invalid email or temporary password.",
            cancellationToken);

        if (!verification.IsSuccess)
        {
            try
            {
                await keycloakAdmin.SetRequiredActionsAsync(
                    user.Id,
                    new[] { FirstLoginRequiredAction },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to restore UPDATE_PASSWORD action for {Email} after invalid first-login attempt.",
                    request.Email);
            }

            logger.LogWarning(
                "First-login password change failed for {Email}: temporary password did not verify.",
                request.Email);
            return verification;
        }

        await keycloakAdmin.ResetPasswordAsync(user.Id, request.NewPassword, temporary: false, cancellationToken);

        // Completing the first-login password change proves the owner received
        // the emailed credentials, so mark the account verified — both the
        // Keycloak IdP flag and the dineOS tenant record. Best-effort: the
        // password rotation has already succeeded, so a failure stamping
        // verification must not fail the request (the 6-digit code flow remains
        // as a fallback).
        try
        {
            await keycloakAdmin.SetEmailVerifiedAsync(user.Id, true, cancellationToken);
            await emailVerification.MarkOwnerEmailVerifiedAsync(request.Email, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "First-login password rotated for {Email} but marking the account verified failed.",
                request.Email);
        }

        // Re-issue a token pair against the new password so the FE doesn't
        // have to call /auth/login separately. The token from the
        // verification step above was issued against the temp password and
        // is fine to return, but rotating here keeps the password used in
        // the active token consistent with what the user just chose.
        var loginForm = CreateClientForm();
        loginForm["grant_type"] = string.IsNullOrWhiteSpace(_options.GrantType) ? "password" : _options.GrantType;
        loginForm["username"] = request.Email;
        loginForm["password"] = request.NewPassword;

        var loginResult = await ExchangeTokenAsync(
            loginForm,
            "Password updated but automatic login failed. Please sign in manually.",
            cancellationToken);

        logger.LogInformation(
            "First-login password rotated for tenant owner {Email} ({UserId}).",
            request.Email, user.Id);

        return loginResult;
    }

    public async Task<Result> ChangePasswordAsync(
        string email,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure("User identity could not be resolved from the token.");

        var validation = await changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

        var verifyForm = CreateClientForm();
        verifyForm["grant_type"] = string.IsNullOrWhiteSpace(_options.GrantType) ? "password" : _options.GrantType;
        verifyForm["username"] = email;
        verifyForm["password"] = request.CurrentPassword;

        var verification = await ExchangeTokenAsync(
            verifyForm,
            "Current password is incorrect.",
            cancellationToken);

        if (!verification.IsSuccess)
        {
            logger.LogWarning("Change-password failed for {Email}: current password did not verify.", email);
            return Result.Failure(verification.Error ?? "Current password is incorrect.");
        }

        var user = await keycloakAdmin.FindUserByEmailAsync(email, cancellationToken);
        if (user is null)
            return Result.Failure("Account not found.");

        await keycloakAdmin.ResetPasswordAsync(user.Id, request.NewPassword, temporary: false, cancellationToken);

        logger.LogInformation("Password changed for user {Email}.", email);
        return Result.Success();
    }

    public async Task<Result> ResetForgottenPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

        var codeResult = await emailVerification.ConsumePasswordResetCodeAsync(
            request.Email, request.Code, cancellationToken);
        if (!codeResult.IsSuccess)
            return Result.Failure(
                codeResult.Message ?? "Reset code is invalid or expired. Request a new code and try again.");

        var user = await keycloakAdmin.FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // The account vanished between code issuance and redemption. Reply
            // with the same constant message as a bad code — never confirm
            // whether an account exists from this endpoint.
            logger.LogWarning("Password reset code verified but no Keycloak user matches the email.");
            return Result.Failure("Reset code is invalid or expired. Request a new code and try again.");
        }

        await keycloakAdmin.ResetPasswordAsync(user.Id, request.NewPassword, temporary: false, cancellationToken);

        // Receiving the code proves inbox ownership: clear any pending
        // UPDATE_PASSWORD action (an owner who lost the temp password recovers
        // here without the first-login flow) and mark the email verified.
        // Best-effort — the new password is already active, so cleanup
        // failures must not fail the request.
        try
        {
            if (user.RequiredActions.Count > 0)
                await keycloakAdmin.SetRequiredActionsAsync(user.Id, Array.Empty<string>(), cancellationToken);
            await keycloakAdmin.SetEmailVerifiedAsync(user.Id, true, cancellationToken);
            await emailVerification.MarkOwnerEmailVerifiedAsync(request.Email, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Post-reset account cleanup failed for {Email}; the new password is already active.",
                request.Email);
        }

        logger.LogInformation("Forgotten password reset completed for user {Email}.", request.Email);
        return Result.Success();
    }

    public async Task<Result> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(
                ValidationFailedMessage,
                validation.Errors.Select(e => e.ErrorMessage).ToList());

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
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Keycloak token endpoint returned {StatusCode} for grant_type {GrantType}.",
                    (int)response.StatusCode,
                    form.GetValueOrDefault("grant_type"));

                // Surface the "Account is not fully set up" condition so the
                // controller/FE can redirect new tenant owners (#205) through
                // the first-login password-change flow rather than show a
                // generic "invalid credentials" message.
                if (IsAccountNotFullySetUp(errorBody))
                {
                    return Result<RefreshTokenResponse>.Failure("Account requires first-login password change.");
                }

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

    private static bool IsAccountNotFullySetUp(string? body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var desc))
            {
                var text = desc.GetString();
                return text is not null
                    && text.Contains("Account is not fully set up", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
            // fall through
        }
        return body.Contains("Account is not fully set up", StringComparison.OrdinalIgnoreCase);
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
