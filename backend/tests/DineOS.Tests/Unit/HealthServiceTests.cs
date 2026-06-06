using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;

namespace DineOS.Tests.Unit;

public class HealthServiceTests
{
    private static HealthService Build(bool redisConnected)
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            Substitute.For<ITenantService>());

        // The EF InMemory provider always reports it can connect, so the database
        // probe resolves to "up" here — exercising the Healthy / Degraded branches.
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.IsConnected.Returns(redisConnected);

        return new HealthService(db, redis, Substitute.For<ILogger<HealthService>>());
    }

    [Fact]
    public async Task GetStatusAsync_AllDependenciesUp_ReturnsHealthy()
    {
        var service = Build(redisConnected: true);

        var status = await service.GetStatusAsync();

        Assert.Equal("Healthy", status.Status);
        Assert.NotNull(status.Version);
        Assert.NotNull(status.Components);
        Assert.Equal("up", status.Components!["database"]);
        Assert.Equal("up", status.Components!["redis"]);
    }

    [Fact]
    public async Task GetStatusAsync_RedisDown_ReturnsDegraded()
    {
        var service = Build(redisConnected: false);

        var status = await service.GetStatusAsync();

        Assert.Equal("Degraded", status.Status);
        Assert.Equal("up", status.Components!["database"]);
        Assert.Equal("down", status.Components!["redis"]);
    }

    [Fact]
    public async Task GetStatusAsync_TimestampIsRecentUtc()
    {
        var service = Build(redisConnected: true);
        var before = DateTime.UtcNow;

        var status = await service.GetStatusAsync();

        Assert.True(status.Timestamp >= before);
        Assert.Equal(DateTimeKind.Utc, status.Timestamp.Kind);
    }
}
