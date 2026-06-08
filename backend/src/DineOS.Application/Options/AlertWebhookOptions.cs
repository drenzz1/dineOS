namespace DineOS.Application.Options;

public sealed class AlertWebhookOptions
{
    public const string SectionName = "AlertWebhook";

    /// <summary>
    /// Optional shared secret. When set, every inbound request to
    /// POST /api/v1/alerts/webhook must carry a matching X-Webhook-Secret
    /// header. Mismatches are logged and the payload is dropped, but the
    /// endpoint still returns 200 so Alertmanager does not retry.
    /// Leave empty to allow unauthenticated access (useful in air-gapped
    /// clusters where network policy already restricts the caller).
    /// </summary>
    public string SharedSecret { get; init; } = string.Empty;
}
