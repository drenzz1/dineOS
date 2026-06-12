using DineOS.Api.Controllers;
using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class AuthControllerTests
{
    private readonly IKeycloakAuthService _authService = Substitute.For<IKeycloakAuthService>();
    private readonly IStaffSessionService _staffSessionService = Substitute.For<IStaffSessionService>();
    private readonly IBackgroundJobClient _backgroundJobs = Substitute.For<IBackgroundJobClient>();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(
            _authService,
            _staffSessionService,
            _backgroundJobs,
            new ForgotPasswordRequestValidator(),
            Options.Create(new KeycloakOptions
            {
                FrontendUrl = "http://localhost:3000",
                GoogleCallbackUrl = "http://localhost:5138/api/v1/auth/google/callback"
            }));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public void GoogleLogin_SetsStateCookieAndRedirectsToKeycloak()
    {
        _authService.BuildGoogleAuthorizationUrl(Arg.Any<string>())
            .Returns(call => $"http://localhost:8080/google?state={call.Arg<string>()}");

        var result = _controller.GoogleLogin("/reports");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("http://localhost:8080/google?state=", redirect.Url);
        Assert.Contains(
            _controller.Response.Headers.SetCookie,
            cookie => cookie!.StartsWith("dineos_google_oauth_state=", StringComparison.Ordinal)
                      && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            _controller.Response.Headers.SetCookie,
            cookie => cookie!.StartsWith("dineos_google_oauth_from=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoogleCallback_WithMatchingState_SetsSessionCookiesAndRedirectsToFrontend()
    {
        _controller.Request.Headers.Cookie =
            "dineos_google_oauth_state=matching-state; dineos_google_oauth_from=%2Freports";
        _authService.ExchangeGoogleAuthorizationCodeAsync(
                "authorization-code",
                Arg.Any<CancellationToken>())
            .Returns(Result<RefreshTokenResponse>.Success(
                new RefreshTokenResponse("access", "refresh", 300, 1800)));

        var result = await _controller.GoogleCallback(
            "authorization-code",
            "matching-state",
            null,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("http://localhost:3000/auth/callback?from=%2Freports", redirect.Url);
        Assert.Contains(
            _controller.Response.Headers.SetCookie,
            cookie => cookie!.StartsWith("access_token=access", StringComparison.Ordinal));
        Assert.Contains(
            _controller.Response.Headers.SetCookie,
            cookie => cookie!.StartsWith("refresh_token=refresh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoogleCallback_WithMismatchedState_DoesNotExchangeCode()
    {
        _controller.Request.Headers.Cookie = "dineos_google_oauth_state=expected";

        var result = await _controller.GoogleCallback(
            "authorization-code",
            "unexpected",
            null,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(
            "http://localhost:3000/auth/callback?error=invalid_oauth_state",
            redirect.Url);
        await _authService.DidNotReceive().ExchangeGoogleAuthorizationCodeAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokenPair()
    {
        var request = new LoginRequest("admin@dineos.dev", "Test1234!");
        var response = new RefreshTokenResponse("access", "refresh", 300, 1800);

        _authService.LoginAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<RefreshTokenResponse>.Success(response));

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<RefreshTokenResponse>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("access", envelope.Data!.AccessToken);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var request = new LoginRequest("admin@dineos.dev", "wrong");

        _authService.LoginAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<RefreshTokenResponse>.Failure("Invalid username or password."));

        var result = await _controller.Login(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_Returns401()
    {
        var request = new RefreshTokenRequest("old-refresh");

        _authService.RefreshAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<RefreshTokenResponse>.Failure("Refresh token has been revoked."));

        var result = await _controller.Refresh(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Logout_WithValidAuthenticatedRequest_Returns204()
    {
        var request = new LogoutRequest("refresh");

        _authService.LogoutAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _controller.Logout(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
