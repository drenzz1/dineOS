namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Runtime feature-flag evaluation. Backed by Unleash where it is configured
/// (<c>Unleash:Enabled=true</c>); otherwise a safe no-op that always returns the
/// caller-supplied default, so the absence (or outage) of a flag provider never
/// changes behaviour.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>
    /// Returns whether <paramref name="flag"/> is enabled. If the provider is
    /// unreachable or the flag is unknown, returns <paramref name="defaultValue"/>.
    /// </summary>
    bool IsEnabled(string flag, bool defaultValue = false);
}

/// <summary>Well-known feature-flag keys — must match the toggle names defined in Unleash.</summary>
public static class FeatureFlag
{
    /// <summary>
    /// AI menu-description generation. Kill-switch semantics: defaults <b>ON</b>; flip
    /// it OFF in Unleash to disable AI generation at runtime (e.g. to cap provider
    /// spend or stop abuse) with no redeploy.
    /// </summary>
    public const string AiMenuGeneration = "ai-menu-generation";
}
