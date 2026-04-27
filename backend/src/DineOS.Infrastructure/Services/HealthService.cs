using DineOS.Application.Interfaces.Services;
using System.Reflection;

namespace DineOS.Infrastructure.Services;

public class HealthService : IHealthService
{
    public Task<HealthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        return Task.FromResult(new HealthStatus("Healthy", DateTime.UtcNow, version));
    }
}
