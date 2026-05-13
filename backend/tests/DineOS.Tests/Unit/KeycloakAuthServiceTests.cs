using DineOS.Application.Authentication;
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

    private KeycloakAuthService CreateService(RecordingHandler handler, KeycloakOptions? options = null)
    {
        var client = new HttpClient(handler);
        var factory = new SingleClientFactory(client);

        return new KeycloakAuthService(
            factory,
            Options.Create(options ?? DefaultOptions()),
            _blacklist,
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            new LogoutRequestValidator(),
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
        GrantType = "password"
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object? body = null)
    {
        var json = body is null ? string.Empty : JsonSerializer.Serialize(body);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string MakeJwt(string jti, long exp)
    {
        var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64Url($"{{\"jti\":\"{jti}\",\"exp\":{exp}}}");
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
