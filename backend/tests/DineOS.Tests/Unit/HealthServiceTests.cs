using DineOS.Infrastructure.Services;

namespace DineOS.Tests.Unit;

public class HealthServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsHealthyStatus()
    {
        var service = new HealthService();

        var status = await service.GetStatusAsync();

        Assert.Equal("Healthy", status.Status);
        Assert.NotNull(status.Version);
    }

    [Fact]
    public async Task GetStatusAsync_TimestampIsRecentUtc()
    {
        var service = new HealthService();
        var before = DateTime.UtcNow;

        var status = await service.GetStatusAsync();

        Assert.True(status.Timestamp >= before);
        Assert.Equal(DateTimeKind.Utc, status.Timestamp.Kind);
    }

    [Fact]
    public async Task GetStatusAsync_RespectsExternalCancellation()
    {
        var service = new HealthService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var status = await service.GetStatusAsync(cts.Token);

        Assert.Equal("Healthy", status.Status);
    }
}
