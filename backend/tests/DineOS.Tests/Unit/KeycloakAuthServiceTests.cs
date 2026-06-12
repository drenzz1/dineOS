using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Unit;

public class KeycloakAuthServiceTests
{
    private readonly ITokenBlacklistService _blacklist = Substitute.For<ITokenBlacklistService>();
    private readonly IKeycloakAdminClient _admin = Substitute.For<IKeycloakAdminClient>();
    private readonly IEmailVerificationService _emailVerification = Substitute.For<IEmailVerificationService>();

    [Fact]
    public void BuildGoogleAuthorizationUrl_UsesConfidentialClientCallbackAndProviderHint()
    {
        var sut = CreateService(new RecordingHandler());

        var url = new Uri(sut.BuildGoogleAuthorizationUrl("state-value"));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal(
            "http://localhost:8080/realms/dineos/protocol/openid-connect/auth",
            url.GetLeftPart(UriPartial.Path));
        Assert.Equal("dineos-google", query["client_id"]);
        Assert.Equal("http://localhost:5138/api/v1/auth/google/callback", query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("openid profile email", query["scope"]);
        Assert.Equal("google", query["kc_idp_hint"]);
        Assert.Equal("state-value", query["state"]);
    }

    [Fact]
    public async Task ExchangeGoogleAuthorizationCodeAsync_UsesAuthorizationCodeGrant()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.OK, new
        {
            access_token = "google-access",
            refresh_token = "google-refresh",
            expires_in = 300,
            refresh_expires_in = 1800
        }));
        var sut = CreateService(handler);

        var result = await sut.ExchangeGoogleAuthorizationCodeAsync("authorization-code");

        Assert.True(result.IsSuccess);
        Assert.Equal("google-access", result.Value!.AccessToken);

        var request = Assert.Single(handler.Requests);
        var form = ParseForm(request.Body);
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("authorization-code", form["code"]);
        Assert.Equal("dineos-google", form["client_id"]);
        Assert.Equal("dev-google-auth-client-secret-change-me", form["client_secret"]);
        Assert.Equal("http://localhost:5138/api/v1/auth/google/callback", form["redirect_uri"]);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenPairAndUsesPasswordGrant()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.OK, new
        {
            access_token = "access-token",
            refresh_token = "refresh-token",
            expires_in = 300,
            refresh_expires_in = 1800
        }));
        var sut = CreateService(handler);

        var result = await sut.LoginAsync(new LoginRequest("admin@dineos.dev", "Test1234!"));

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value!.AccessToken);
        Assert.Equal(1800, result.Value.RefreshExpiresIn);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "http://keycloak:8080/realms/dineos/protocol/openid-connect/token",
            request.RequestUri.ToString());

        var form = ParseForm(request.Body);
        Assert.Equal("password", form["grant_type"]);
        Assert.Equal("dineos-frontend", form["client_id"]);
        Assert.Equal("admin@dineos.dev", form["username"]);
        Assert.Equal("Test1234!", form["password"]);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ReturnsFailure()
    {
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.BadRequest));
        var sut = CreateService(handler);

        var result = await sut.LoginAsync(new LoginRequest("admin@dineos.dev", "wrong"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Fact]
    public async Task RefreshAsync_WithRevokedToken_ReturnsFailureWithoutCallingKeycloak()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = MakeJwt(jti, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var handler = new RecordingHandler();
        var sut = CreateService(handler);

        _blacklist.IsBlacklistedAsync(jti).Returns(true);

        var result = await sut.RefreshAsync(new RefreshTokenRequest(refreshToken));

        Assert.False(result.IsSuccess);
        Assert.Equal("Refresh token has been revoked.", result.Error);
        Assert.Empty(handler.Requests);
        await _blacklist.DidNotReceive().BlacklistAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_BlacklistsOldTokenAndReturnsNewPair()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = MakeJwt(jti, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.OK, new
        {
            access_token = "new-access",
            refresh_token = "new-refresh",
            expires_in = 300
        }));
        var sut = CreateService(handler);

        _blacklist.IsBlacklistedAsync(jti).Returns(false);

        var result = await sut.RefreshAsync(new RefreshTokenRequest(refreshToken));

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access", result.Value!.AccessToken);

        var request = Assert.Single(handler.Requests);
        var form = ParseForm(request.Body);
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("dineos-frontend", form["client_id"]);
        Assert.Equal(refreshToken, form["refresh_token"]);

        await _blacklist.Received(1).BlacklistAsync(
            jti,
            Arg.Is<TimeSpan>(ttl => ttl > TimeSpan.Zero));
    }

    [Fact]
    public async Task RefreshAsync_WithGoogleClientToken_UsesGoogleClientCredentials()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = MakeJwt(
            jti,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            "dineos-google");
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.OK, new
        {
            access_token = "new-google-access",
            refresh_token = "new-google-refresh",
            expires_in = 300
        }));
        var sut = CreateService(handler);

        _blacklist.IsBlacklistedAsync(jti).Returns(false);

        var result = await sut.RefreshAsync(new RefreshTokenRequest(refreshToken));

        Assert.True(result.IsSuccess);
        var request = Assert.Single(handler.Requests);
        var form = ParseForm(request.Body);
        Assert.Equal("dineos-google", form["client_id"]);
        Assert.Equal("dev-google-auth-client-secret-change-me", form["client_secret"]);
    }

    [Fact]
    public async Task RefreshAsync_WhenKeycloakRejectsToken_ReturnsFailure()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = MakeJwt(jti, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.BadRequest));
        var sut = CreateService(handler);

        _blacklist.IsBlacklistedAsync(jti).Returns(false);

        var result = await sut.RefreshAsync(new RefreshTokenRequest(refreshToken));

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
        await _blacklist.DidNotReceive().BlacklistAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task LogoutAsync_BlacklistsTokenAndCallsRevocationEndpoint()
    {
        var jti = Guid.NewGuid().ToString();
        var refreshToken = MakeJwt(jti, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.NoContent));
        var sut = CreateService(handler);

        var result = await sut.LogoutAsync(new LogoutRequest(refreshToken));

        Assert.True(result.IsSuccess);
        await _blacklist.Received(1).BlacklistAsync(
            jti,
            Arg.Is<TimeSpan>(ttl => ttl > TimeSpan.Zero));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "http://keycloak:8080/realms/dineos/protocol/openid-connect/revoke",
            request.RequestUri.ToString());

        var form = ParseForm(request.Body);
        Assert.Equal("dineos-frontend", form["client_id"]);
        Assert.Equal(refreshToken, form["token"]);
        Assert.Equal("refresh_token", form["token_type_hint"]);
    }

    [Fact]
    public async Task LogoutAsync_WithGoogleClientToken_UsesGoogleClientCredentials()
    {
        var refreshToken = MakeJwt(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            "dineos-google");
        var handler = new RecordingHandler(JsonResponse(HttpStatusCode.NoContent));
        var sut = CreateService(handler);

        var result = await sut.LogoutAsync(new LogoutRequest(refreshToken));

        Assert.True(result.IsSuccess);
        var request = Assert.Single(handler.Requests);
        var form = ParseForm(request.Body);
        Assert.Equal("dineos-google", form["client_id"]);
        Assert.Equal("dev-google-auth-client-secret-change-me", form["client_secret"]);
    }

    [Fact]
    public async Task ChangeFirstLoginPasswordAsync_OnSuccess_MarksEmailVerifiedInKeycloakAndTenant()
    {
        _admin.FindUserByEmailAsync("owner@dineos.dev", Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserSummary("user-123", new[] { "UPDATE_PASSWORD" }));

        // Two token exchanges happen: (1) verify the temporary password,
        // (2) re-login against the freshly chosen password.
        var handler = new RecordingHandler(
            JsonResponse(HttpStatusCode.OK, new { access_token = "a1", refresh_token = "r1", expires_in = 300 }),
            JsonResponse(HttpStatusCode.OK, new { access_token = "a2", refresh_token = "r2", expires_in = 300 }));
        var sut = CreateService(handler);

        var result = await sut.ChangeFirstLoginPasswordAsync(
            new FirstLoginPasswordChangeRequest("owner@dineos.dev", "TempPass-123", "BrandNewPass-456"));

        Assert.True(result.IsSuccess);
        await _admin.Received(1).SetEmailVerifiedAsync("user-123", true, Arg.Any<CancellationToken>());
        await _emailVerification.Received(1)
            .MarkOwnerEmailVerifiedAsync("owner@dineos.dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeFirstLoginPasswordAsync_WhenNotInFirstLoginState_DoesNotMarkVerified()
    {
        _admin.FindUserByEmailAsync("owner@dineos.dev", Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserSummary("user-123", Array.Empty<string>()));

        var sut = CreateService(new RecordingHandler());

        var result = await sut.ChangeFirstLoginPasswordAsync(
            new FirstLoginPasswordChangeRequest("owner@dineos.dev", "TempPass-123", "BrandNewPass-456"));

        Assert.False(result.IsSuccess);
        await _admin.DidNotReceive().SetEmailVerifiedAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await _emailVerification.DidNotReceive()
            .MarkOwnerEmailVerifiedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetForgottenPasswordAsync_WithValidCode_ResetsPasswordAndClearsRequiredActions()
    {
        _emailVerification.ConsumePasswordResetCodeAsync("owner@dineos.dev", "123456", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Ok(true));
        // An owner who lost the emailed temp password still has UPDATE_PASSWORD
        // pending — the reset must clear it so direct-grant login works after.
        _admin.FindUserByEmailAsync("owner@dineos.dev", Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserSummary("user-123", new[] { "UPDATE_PASSWORD" }));

        var sut = CreateService(new RecordingHandler());

        var result = await sut.ResetForgottenPasswordAsync(
            new ResetPasswordRequest("owner@dineos.dev", "123456", "BrandNewPass-456"));

        Assert.True(result.IsSuccess);
        await _admin.Received(1).ResetPasswordAsync(
            "user-123", "BrandNewPass-456", temporary: false, Arg.Any<CancellationToken>());
        await _admin.Received(1).SetRequiredActionsAsync(
            "user-123", Arg.Is<IReadOnlyList<string>>(a => a.Count == 0), Arg.Any<CancellationToken>());
        await _admin.Received(1).SetEmailVerifiedAsync("user-123", true, Arg.Any<CancellationToken>());
        await _emailVerification.Received(1)
            .MarkOwnerEmailVerifiedAsync("owner@dineos.dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetForgottenPasswordAsync_WithInvalidCode_DoesNotTouchKeycloak()
    {
        const string invalid = "Reset code is invalid or expired. Request a new code and try again.";
        _emailVerification.ConsumePasswordResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.ValidationFailed(invalid, new List<string> { invalid }));

        var sut = CreateService(new RecordingHandler());

        var result = await sut.ResetForgottenPasswordAsync(
            new ResetPasswordRequest("owner@dineos.dev", "000000", "BrandNewPass-456"));

        Assert.False(result.IsSuccess);
        Assert.Equal(invalid, result.Error);
        await _admin.DidNotReceive().ResetPasswordAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetForgottenPasswordAsync_WhenNoUserMatches_FailsWithSameConstantMessage()
    {
        // The account vanished between code issuance and redemption — the
        // response must be indistinguishable from a bad code (no enumeration).
        _emailVerification.ConsumePasswordResetCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<bool>.Ok(true));
        _admin.FindUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((KeycloakUserSummary?)null);

        var sut = CreateService(new RecordingHandler());

        var result = await sut.ResetForgottenPasswordAsync(
            new ResetPasswordRequest("ghost@dineos.dev", "123456", "BrandNewPass-456"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Reset code is invalid or expired. Request a new code and try again.", result.Error);
    }

    private KeycloakAuthService CreateService(RecordingHandler handler, KeycloakOptions? options = null)
    {
        var client = new HttpClient(handler);
        var factory = new SingleClientFactory(client);

        return new KeycloakAuthService(
            factory,
            Options.Create(options ?? DefaultOptions()),
            _blacklist,
            _admin,
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            new LogoutRequestValidator(),
            new FirstLoginPasswordChangeRequestValidator(),
            new ChangePasswordRequestValidator(),
            new ResetPasswordRequestValidator(),
            _emailVerification,
            NullLogger<KeycloakAuthService>.Instance);
    }

    private static KeycloakOptions DefaultOptions() => new()
    {
        Realm = "dineos",
        Authority = "http://localhost:8080/realms/dineos",
        AuthServerUrl = "http://keycloak:8080",
        PublicAuthServerUrl = "http://localhost:8080",
        Audience = "dineos-api",
        ClientId = "dineos-frontend",
        GrantType = "password",
        GoogleClientId = "dineos-google",
        GoogleClientSecret = "dev-google-auth-client-secret-change-me",
        GoogleCallbackUrl = "http://localhost:5138/api/v1/auth/google/callback",
        FrontendUrl = "http://localhost:3000"
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object? body = null)
    {
        var json = body is null ? string.Empty : JsonSerializer.Serialize(body);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string MakeJwt(string jti, long exp, string? authorizedParty = null)
    {
        var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64Url(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jti"] = jti,
            ["exp"] = exp,
            ["azp"] = authorizedParty
        }));
        return $"{header}.{payload}.";
    }

    private static string B64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static Dictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => DecodeFormValue(part[0]),
                part => part.Length > 1 ? DecodeFormValue(part[1]) : string.Empty);

    private static string DecodeFormValue(string value) =>
        Uri.UnescapeDataString(value.Replace("+", " "));

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException("No fake HTTP response was configured.");

            return _responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri RequestUri, string Body);
}
