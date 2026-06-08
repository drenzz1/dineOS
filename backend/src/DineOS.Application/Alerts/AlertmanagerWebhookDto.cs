namespace DineOS.Application.Alerts;

/// <summary>
/// Standard Alertmanager v4 webhook payload.
/// https://prometheus.io/docs/alerting/latest/configuration/#webhook_config
/// </summary>
public sealed record AlertmanagerWebhookPayload
{
    public string Version { get; init; } = string.Empty;
    public string GroupKey { get; init; } = string.Empty;
    public int TruncatedAlerts { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Receiver { get; init; } = string.Empty;
    public Dictionary<string, string> GroupLabels { get; init; } = new();
    public Dictionary<string, string> CommonLabels { get; init; } = new();
    public Dictionary<string, string> CommonAnnotations { get; init; } = new();
    public string ExternalURL { get; init; } = string.Empty;
    public IReadOnlyList<AlertmanagerAlert> Alerts { get; init; } = Array.Empty<AlertmanagerAlert>();
}

public sealed record AlertmanagerAlert
{
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, string> Labels { get; init; } = new();
    public Dictionary<string, string> Annotations { get; init; } = new();
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public string GeneratorURL { get; init; } = string.Empty;
    public string Fingerprint { get; init; } = string.Empty;
}
