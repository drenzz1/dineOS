using System.Diagnostics;
using DineOS.Application.Alerts;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public sealed class IncidentTriageService(
    IAiClient aiClient,
    ISlackNotifier slackNotifier,
    ILogger<IncidentTriageService> logger) : IIncidentTriageService
{
    // Label/annotation key substrings that indicate the value should be redacted.
    private static readonly string[] SensitiveKeySubstrings =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
        "credential", "connectionstring", "conn_string", "connstring", "auth"
    ];

    // Value fragments that suggest connection-string or credential data,
    // checked only when the value is long enough to be suspicious (> 30 chars).
    private static readonly string[] SensitiveValueSubstrings =
    [
        "password=", "pwd=", "://", ";database=", "initial catalog=",
        "user id=", "uid=", "data source="
    ];

    public async Task<IReadOnlyList<IncidentTriageResultDto>> ProcessWebhookAsync(
        AlertmanagerWebhookPayload payload,
        CancellationToken ct = default)
    {
        var results = new List<IncidentTriageResultDto>(payload.Alerts.Count);

        foreach (var alert in payload.Alerts)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var alertName     = GetLabel(alert.Labels, payload.CommonLabels, "alertname",
                                         payload.Receiver.Length > 0 ? payload.Receiver : "unknown");

            var sw = Stopwatch.StartNew();
            try
            {
                var request = BuildTriageRequest(alert, payload);

                logger.LogInformation(
                    "Incident triage started: CorrelationId={CorrelationId} AlertName={AlertName} " +
                    "Severity={Severity} Status={Status}",
                    correlationId, alertName, request.Severity, alert.Status);

                var result = await aiClient.TriageIncidentAsync(request, ct);
                sw.Stop();

                logger.LogInformation(
                    "Incident triage completed: CorrelationId={CorrelationId} AlertName={AlertName} " +
                    "Severity={Severity} Provider={Provider} InputTokens={InputTokens} " +
                    "OutputTokens={OutputTokens} LatencyMs={LatencyMs} Outcome=Success",
                    correlationId, alertName, result.Severity, result.Usage.Model,
                    result.Usage.InputTokens, result.Usage.OutputTokens, sw.ElapsedMilliseconds);

                var dto = new IncidentTriageResultDto(
                    CorrelationId:        correlationId,
                    AlertName:            alertName,
                    Severity:             result.Severity,
                    LikelyCauses:         result.LikelyCauses,
                    SuggestedNextActions: result.SuggestedNextActions,
                    ShortSummary:         result.ShortSummary,
                    Usage:                result.Usage);

                results.Add(dto);

                await slackNotifier.NotifyTriageAsync(dto, ct);
            }
            catch (AiUnavailableException ex)
            {
                sw.Stop();
                logger.LogWarning(ex,
                    "Incident triage failed: CorrelationId={CorrelationId} AlertName={AlertName} " +
                    "Outcome=AiUnavailable LatencyMs={LatencyMs}",
                    correlationId, alertName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex,
                    "Incident triage unexpected error: CorrelationId={CorrelationId} " +
                    "AlertName={AlertName} Outcome=Error LatencyMs={LatencyMs}",
                    correlationId, alertName, sw.ElapsedMilliseconds);
            }
        }

        return results;
    }

    // ── Request normalisation ─────────────────────────────────────────────

    private static IncidentTriageAiRequest BuildTriageRequest(
        AlertmanagerAlert alert,
        AlertmanagerWebhookPayload payload)
    {
        var alertName   = GetLabel(alert.Labels, payload.CommonLabels, "alertname",
                                   payload.Receiver.Length > 0 ? payload.Receiver : "unknown");
        var severity    = GetLabel(alert.Labels, payload.CommonLabels, "severity", "unknown");
        var component   = ResolveComponent(alert, payload);
        var summary     = GetAnnotation(alert.Annotations, payload.CommonAnnotations, "summary");
        var description = GetAnnotation(alert.Annotations, payload.CommonAnnotations, "description");
        var labels      = SanitizeLabels(alert.Labels);

        return new IncidentTriageAiRequest(
            AlertName:   alertName,
            Severity:    severity,
            Component:   component,
            Status:      alert.Status,
            Summary:     summary,
            Description: description,
            Labels:      labels,
            FiringSince: alert.StartsAt);
    }

    private static string ResolveComponent(AlertmanagerAlert alert, AlertmanagerWebhookPayload payload)
    {
        foreach (var key in (string[])["job", "service", "component", "instance"])
        {
            var v = GetLabel(alert.Labels, payload.CommonLabels, key, string.Empty);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return "unknown";
    }

    // ── Label / annotation helpers ────────────────────────────────────────

    private static IReadOnlyList<KeyValuePair<string, string>> SanitizeLabels(
        Dictionary<string, string> labels) =>
        labels
            .Select(kv => new KeyValuePair<string, string>(kv.Key, RedactIfSensitive(kv.Key, kv.Value)))
            .ToList();

    private static string RedactIfSensitive(string key, string value)
    {
        if (SensitiveKeySubstrings.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return "[REDACTED]";

        if (value.Length > 30 &&
            SensitiveValueSubstrings.Any(s => value.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return "[REDACTED]";

        return value;
    }

    private static string GetLabel(
        Dictionary<string, string> alertLabels,
        Dictionary<string, string> commonLabels,
        string key,
        string fallback = "unknown")
    {
        if (alertLabels.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v;
        if (commonLabels.TryGetValue(key, out var c) && !string.IsNullOrWhiteSpace(c))
            return c;
        return fallback;
    }

    private static string GetAnnotation(
        Dictionary<string, string> alertAnnotations,
        Dictionary<string, string> commonAnnotations,
        string key,
        string fallback = "(none)")
    {
        if (alertAnnotations.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v;
        if (commonAnnotations.TryGetValue(key, out var c) && !string.IsNullOrWhiteSpace(c))
            return c;
        return fallback;
    }
}
