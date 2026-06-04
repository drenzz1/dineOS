using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DineOS.Application.Alerts;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public sealed class SlackNotifier(
    HttpClient http,
    IOptions<SlackOptions> options,
    ILogger<SlackNotifier> logger) : ISlackNotifier
{
    public const string HttpClientName = "slack";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly SlackOptions _opts = options.Value;

    public async Task NotifyTriageAsync(IncidentTriageResultDto result, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.WebhookUrl))
        {
            logger.LogWarning(
                "Slack WebhookUrl is not configured — skipping notification. " +
                "Set Slack:WebhookUrl (or SLACK__WEBHOOKURL env var) to enable Slack alerts.");
            return;
        }

        var payload = BuildPayload(result);

        try
        {
            using var response = await http.PostAsJsonAsync(_opts.WebhookUrl, payload, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Slack notification failed: StatusCode={StatusCode} Body={Body} " +
                    "CorrelationId={CorrelationId} AlertName={AlertName}",
                    (int)response.StatusCode, body, result.CorrelationId, result.AlertName);
            }
            else
            {
                logger.LogInformation(
                    "Slack notification sent: CorrelationId={CorrelationId} AlertName={AlertName}",
                    result.CorrelationId, result.AlertName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Slack notification threw an exception: CorrelationId={CorrelationId} AlertName={AlertName}",
                result.CorrelationId, result.AlertName);
        }
    }

    private static object BuildPayload(IncidentTriageResultDto r)
    {
        var likelyCauses = r.LikelyCauses.Count > 0
            ? string.Join("\n", r.LikelyCauses.Select((c, i) => $"{i + 1}. {c}"))
            : "(none)";

        var nextActions = r.SuggestedNextActions.Count > 0
            ? string.Join("\n", r.SuggestedNextActions.Select((a, i) => $"{i + 1}. {a}"))
            : "(none)";

        var severityEmoji = r.Severity.ToLowerInvariant() switch
        {
            "critical" => ":red_circle:",
            "high"     => ":large_orange_circle:",
            "medium"   => ":large_yellow_circle:",
            _          => ":white_circle:",
        };

        return new
        {
            text = $"{severityEmoji} *Incident Triage — {r.AlertName}*",
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = $"Incident Triage: {r.AlertName}", emoji = true },
                },
                new
                {
                    type = "section",
                    fields = new[]
                    {
                        new { type = "mrkdwn", text = $"*Alert*\n{r.AlertName}" },
                        new { type = "mrkdwn", text = $"*Severity*\n{severityEmoji} {r.Severity}" },
                        new { type = "mrkdwn", text = $"*Correlation ID*\n`{r.CorrelationId}`" },
                        new { type = "mrkdwn", text = $"*AI Model*\n{r.Usage.Model}" },
                    },
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Summary*\n{r.ShortSummary}" },
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Likely Causes*\n{likelyCauses}" },
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Suggested Next Actions*\n{nextActions}" },
                },
                new
                {
                    type = "divider",
                },
            },
        };
    }
}
