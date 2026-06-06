namespace DineOS.Application.Interfaces.Services;

public interface IHealthService
{
    Task<HealthStatus> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>
/// Aggregate API health. <paramref name="Status"/> is "Healthy" when every
/// dependency is reachable, "Degraded" when a non-critical dependency (e.g.
/// Redis) is down but the API can still serve requests, and "Unhealthy" when a
/// critical dependency (the database) is unreachable. <paramref name="Components"/>
/// reports per-dependency state ("up"/"down").
/// </summary>
public record HealthStatus(
    string Status,
    DateTime Timestamp,
    string Version,
    IReadOnlyDictionary<string, string>? Components = null);
