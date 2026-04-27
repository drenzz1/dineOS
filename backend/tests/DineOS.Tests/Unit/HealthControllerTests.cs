using DineOS.Api.Controllers;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class HealthControllerTests
{
    private readonly IHealthService _service = Substitute.For<IHealthService>();
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _controller = new HealthController(_service);
    }

    [Fact]
    public async Task Get_ReturnsOk_WithHealthStatusFromService()
    {
        var expected = new HealthStatus("Healthy", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "1.0.0");
        _service.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<HealthStatus>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("Healthy", envelope.Data!.Status);
        Assert.Equal("1.0.0", envelope.Data.Version);
    }

    [Fact]
    public async Task Get_ForwardsCancellationToken_ToService()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _service.GetStatusAsync(token).Returns(new HealthStatus("Healthy", DateTime.UtcNow, "1.0.0"));

        await _controller.Get(token);

        await _service.Received(1).GetStatusAsync(token);
    }
}
