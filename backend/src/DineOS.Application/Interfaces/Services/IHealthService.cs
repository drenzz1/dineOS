namespace DineOS.Application.Interfaces.Services;

public interface IHealthService
{
    Task<HealthStatus> GetStatusAsync(CancellationToken ct = default);
}

public record HealthStatus(string Status, DateTime Timestamp, string Version);
