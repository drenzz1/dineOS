namespace DineOS.Application.Options;

/// <summary>
/// Configuration for the Unleash feature-flag client. When <see cref="Enabled"/> is
/// false (the default), no Unleash connection is made and feature flags fall back to
/// their per-call default values — identical behaviour to having no flag system.
/// </summary>
public sealed class UnleashOptions
{
    public const string SectionName = "Unleash";

    /// <summary>Master switch — when false the Unleash client is never created.</summary>
    public bool Enabled { get; set; }

    /// <summary>Unleash API base URL, including the trailing <c>/api/</c>.</summary>
    public string ApiUrl { get; set; } = "http://localhost:4242/api/";

    /// <summary>Client (server-side) API token. Encodes the Unleash project + environment.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Application name registered with Unleash (shown in its metrics).</summary>
    public string AppName { get; set; } = "dineos-api";

    /// <summary>How often (seconds) to poll Unleash for toggle changes.</summary>
    public int FetchTogglesIntervalSeconds { get; set; } = 15;
}
