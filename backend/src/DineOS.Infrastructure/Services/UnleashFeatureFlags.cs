using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Unleash;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// <see cref="IFeatureFlags"/> backed by Unleash. Resilient by design: any evaluation
/// error (e.g. the client never reached the server) falls back to the caller-supplied
/// default, so a flag-provider outage can never take down a request path.
/// </summary>
public sealed class UnleashFeatureFlags(IUnleash unleash, ILogger<UnleashFeatureFlags> logger) : IFeatureFlags
{
    public bool IsEnabled(string flag, bool defaultValue = false)
    {
        try
        {
            return unleash.IsEnabled(flag, defaultValue);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Feature-flag evaluation failed for {Flag}; falling back to default {Default}.",
                flag, defaultValue);
            return defaultValue;
        }
    }
}

/// <summary>
/// No-op <see cref="IFeatureFlags"/> used when Unleash is disabled. Always returns the
/// caller-supplied default, so behaviour is identical to "no flag system present".
/// </summary>
public sealed class DefaultFeatureFlags : IFeatureFlags
{
    public bool IsEnabled(string flag, bool defaultValue = false) => defaultValue;
}
