using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Reflection;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// Real readiness probe: pings the database (critical) and Redis (non-critical)
/// instead of returning a hardcoded "Healthy". A load balancer / orchestrator
/// pointed at this endpoint will see a degraded/unhealthy instance rather than
/// keep routing traffic to a broken one.
/// </summary>
public class HealthService(
    AppDbContext db,
    IConnectionMultiplexer redis,
    ILogger<HealthService> logger) : IHealthService
{
    public async Task<HealthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        var dbUp = await ProbeDatabaseAsync(ct);
        var redisUp = ProbeRedis();

        var components = new Dictionary<string, string>
        {
            ["database"] = dbUp ? "up" : "down",
            ["redis"] = redisUp ? "up" : "down",
        };

        // Database is critical (the system of record). Redis is non-critical —
        // the app degrades gracefully (cache-miss to DB, single-node SignalR), so
        // a Redis outage is "Degraded", not "Unhealthy".
        var status = !dbUp ? "Unhealthy"
            : !redisUp ? "Degraded"
            : "Healthy";

        return new HealthStatus(status, DateTime.UtcNow, version, components);
    }

    private async Task<bool> ProbeDatabaseAsync(CancellationToken ct)
    {
        try
        {
            // Bound the probe so a down/slow database fails the readiness check fast
            // instead of blocking on Npgsql's multi-second connection timeout.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            return await db.Database.CanConnectAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check: database probe failed or timed out.");
            return false;
        }
    }

    private bool ProbeRedis()
    {
        try
        {
            return redis.IsConnected;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check: Redis probe failed.");
            return false;
        }
    }
}
