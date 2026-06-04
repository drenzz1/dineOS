namespace DineOS.Application.Options;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>Incoming Webhook URL. Set via SLACK__WEBHOOKURL env var — never commit a real value.</summary>
    public string WebhookUrl    { get; init; } = string.Empty;
    public int    TimeoutSeconds { get; init; } = 10;
}
