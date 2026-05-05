using DineOS.Api.Controllers;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Unit;

public class AuthControllerTests
{
    private readonly ITokenBlacklistService _blacklist = Substitute.For<ITokenBlacklistService>();
    private readonly IHttpClientFactory     _factory   = Substitute.For<IHttpClientFactory>();
    private readonly IConfiguration         _config    = Substitute.For<IConfiguration>();
    private readonly AuthController         _controller;

    public AuthControllerTests()
    {
        _config["Keycloak:Authority"].Returns("http://localhost:8080/realms/dineos");
        _controller = new AuthController(_blacklist, _factory, _config);
    }

    // Creates a minimal unsigned JWT with the given jti and exp claims.
    private static string MakeJwt(string jti, long exp)
    {
        var header  = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = B64Url($"{{\"jti\":\"{jti}\",\"exp\":{exp}}}");
        return $"{header}.{payload}.";
    }

    private static string B64Url(string s)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
                  .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpClient FakeKeycloak(HttpStatusCode status, object? body = null)
    {
        var json    = body is null ? string.Empty : JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return new HttpClient(new StubHandler(new HttpResponseMessage(status) { Content = content }));
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(response);
    }

    [Fact]
    public async Task RefreshToken_WithValidKeycloakResponse_Returns200WithNewTokenPair()
    {
        var jti      = Guid.NewGuid().ToString();
        var exp      = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var oldToken = MakeJwt(jti, exp);

        _blacklist.IsBlacklistedAsync(jti).Returns(false);
        _factory.CreateClient(Arg.Any<string>()).Returns(FakeKeycloak(HttpStatusCode.OK, new
        {
            access_token  = "new-access",
            refresh_token = "new-refresh",
            expires_in    = 300
        }));

        var result   = await _controller.Refresh(new RefreshTokenRequest(oldToken));
        var ok       = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<RefreshTokenResponse>>(ok.Value);

        Assert.True(envelope.Success);
        Assert.Equal("new-access",  envelope.Data!.AccessToken);
        Assert.Equal("new-refresh", envelope.Data.RefreshToken);
        await _blacklist.Received(1).BlacklistAsync(jti, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task RefreshToken_WhenKeycloakReturnsError_ExpiredToken_Returns401()
    {
        var jti      = Guid.NewGuid().ToString();
        var exp      = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var oldToken = MakeJwt(jti, exp);

        _blacklist.IsBlacklistedAsync(jti).Returns(false);
        _factory.CreateClient(Arg.Any<string>()).Returns(FakeKeycloak(HttpStatusCode.BadRequest));

        var result = await _controller.Refresh(new RefreshTokenRequest(oldToken));

        Assert.IsType<UnauthorizedObjectResult>(result);
        await _blacklist.DidNotReceive().BlacklistAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_Returns401()
    {
        var jti          = Guid.NewGuid().ToString();
        var exp          = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var revokedToken = MakeJwt(jti, exp);

        _blacklist.IsBlacklistedAsync(jti).Returns(true);

        var result = await _controller.Refresh(new RefreshTokenRequest(revokedToken));

        Assert.IsType<UnauthorizedObjectResult>(result);
        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
        await _blacklist.DidNotReceive().BlacklistAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Logout_WithValidAuthenticatedRequest_Returns204AndRevokesToken()
    {
        var jti   = Guid.NewGuid().ToString();
        var exp   = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token = MakeJwt(jti, exp);

        var result = await _controller.Logout(new LogoutRequest(token));

        Assert.IsType<NoContentResult>(result);
        await _blacklist.Received(1).BlacklistAsync(
            jti,
            Arg.Is<TimeSpan>(t => t > TimeSpan.Zero));
    }

    [Fact]
    public async Task Logout_WithAlreadyRevokedToken_StillReturns204()
    {
        var jti   = Guid.NewGuid().ToString();
        var exp   = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var token = MakeJwt(jti, exp);

        _blacklist.IsBlacklistedAsync(jti).Returns(true);

        var result = await _controller.Logout(new LogoutRequest(token));

        Assert.IsType<NoContentResult>(result);
        await _blacklist.Received(1).BlacklistAsync(jti, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Logout_WithExpiredToken_StillRevokesAndReturns204()
    {
        var jti   = Guid.NewGuid().ToString();
        var exp   = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
        var token = MakeJwt(jti, exp);

        var result = await _controller.Logout(new LogoutRequest(token));

        Assert.IsType<NoContentResult>(result);
        await _blacklist.Received(1).BlacklistAsync(jti, TimeSpan.Zero);
    }
}
