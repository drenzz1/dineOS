using DineOS.Api.Controllers;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
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
            new ForgotPasswordRequestValidator());
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
