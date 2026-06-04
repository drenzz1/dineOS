using DineOS.Application.Alerts;
using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DineOS.Tests.Unit;

public class IncidentTriageServiceTests
{
    private static (IncidentTriageService svc, IAiClient ai) CreateSut()
    {
        var ai    = Substitute.For<IAiClient>();
        var slack = Substitute.For<ISlackNotifier>();
        var svc   = new IncidentTriageService(ai, slack, NullLogger<IncidentTriageService>.Instance);
        return (svc, ai);
    }

    private static AlertmanagerWebhookPayload BuildPayload(
        string alertName   = "HighErrorRate",
        string severity    = "critical",
        string status      = "firing",
        string summary     = "Error rate above 10%",
        string description = "HTTP 500 responses spiking on /api/orders",
        Dictionary<string, string>? extraLabels = null)
    {
        var labels = new Dictionary<string, string>
        {
            ["alertname"] = alertName,
            ["severity"]  = severity,
            ["job"]       = "dineos-api",
        };
        if (extraLabels is not null)
            foreach (var kv in extraLabels)
                labels[kv.Key] = kv.Value;

        return new AlertmanagerWebhookPayload
        {
            Version            = "4",
            Status             = status,
            Receiver           = "dineos-webhook",
            GroupLabels        = new() { ["alertname"] = alertName },
            CommonLabels       = new() { ["severity"]  = severity },
            CommonAnnotations  = new(),
            Alerts =
            [
                new AlertmanagerAlert
                {
                    Status      = status,
                    Labels      = labels,
                    Annotations = new()
                    {
                        ["summary"]     = summary,
                        ["description"] = description,
                    },
                    StartsAt    = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
                    EndsAt      = DateTimeOffset.MinValue,
                    GeneratorURL = "http://prometheus:9090/graph",
                    Fingerprint  = "abc123",
                }
            ],
        };
    }

    private static IncidentTriageAiResult BuildTriageResult() => new(
        Severity:             "high",
        LikelyCauses:         new[] { "Redis OOM", "Connection pool exhausted" },
        SuggestedNextActions: new[] { "Restart Redis", "Check pod logs" },
        ShortSummary:         "Redis is down causing order write failures.",
        Usage:                new AiUsage(200, 80, "claude-sonnet-test"));

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_HappyPath_ReturnsTriage()
    {
        var (svc, ai) = CreateSut();
        var payload   = BuildPayload();

        ai.TriageIncidentAsync(Arg.Any<IncidentTriageAiRequest>(), Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        var results = await svc.ProcessWebhookAsync(payload);

        Assert.Single(results);
        var r = results[0];
        Assert.Equal("HighErrorRate", r.AlertName);
        Assert.Equal("high", r.Severity);
        Assert.Equal(2, r.LikelyCauses.Count);
        Assert.Contains("Redis OOM", r.LikelyCauses);
        Assert.Equal(2, r.SuggestedNextActions.Count);
        Assert.False(string.IsNullOrWhiteSpace(r.CorrelationId));
        Assert.Equal("claude-sonnet-test", r.Usage.Model);
    }

    [Fact]
    public async Task ProcessWebhookAsync_NormalisesAlertFieldsIntoRequest()
    {
        var (svc, ai) = CreateSut();
        var payload   = BuildPayload(
            summary:     "DB latency spike",
            description: "p99 latency > 2 s on /api/orders");

        IncidentTriageAiRequest? captured = null;
        ai.TriageIncidentAsync(
                Arg.Do<IncidentTriageAiRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        await svc.ProcessWebhookAsync(payload);

        Assert.NotNull(captured);
        Assert.Equal("HighErrorRate",  captured!.AlertName);
        Assert.Equal("critical",        captured.Severity);
        Assert.Equal("dineos-api",      captured.Component);
        Assert.Equal("firing",          captured.Status);
        Assert.Contains("latency spike", captured.Summary);
        Assert.Contains("p99", captured.Description);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero), captured.FiringSince);
    }

    // ── Error paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_AiUnavailable_ReturnsEmptyList()
    {
        var (svc, ai) = CreateSut();

        ai.TriageIncidentAsync(Arg.Any<IncidentTriageAiRequest>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromException<IncidentTriageAiResult>(
              new AiUnavailableException("provider is down")));

        var results = await svc.ProcessWebhookAsync(BuildPayload());

        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessWebhookAsync_UnexpectedError_ReturnsEmptyList()
    {
        var (svc, ai) = CreateSut();

        ai.TriageIncidentAsync(Arg.Any<IncidentTriageAiRequest>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromException<IncidentTriageAiResult>(
              new InvalidOperationException("unexpected")));

        var results = await svc.ProcessWebhookAsync(BuildPayload());

        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessWebhookAsync_EmptyAlerts_ReturnsEmptyList_NoAiCall()
    {
        var (svc, ai) = CreateSut();
        var payload = BuildPayload() with { Alerts = Array.Empty<AlertmanagerAlert>() };

        var results = await svc.ProcessWebhookAsync(payload);

        Assert.Empty(results);
        await ai.DidNotReceiveWithAnyArgs()
                .TriageIncidentAsync(default!, default);
    }

    [Fact]
    public async Task ProcessWebhookAsync_MultipleAlerts_ProcessesAll()
    {
        var (svc, ai) = CreateSut();

        var alert1 = new AlertmanagerAlert
        {
            Status      = "firing",
            Labels      = new() { ["alertname"] = "AlertA", ["severity"] = "high",   ["job"] = "api" },
            Annotations = new() { ["summary"] = "A summary" },
            StartsAt    = DateTimeOffset.UtcNow,
        };
        var alert2 = new AlertmanagerAlert
        {
            Status      = "firing",
            Labels      = new() { ["alertname"] = "AlertB", ["severity"] = "medium", ["job"] = "worker" },
            Annotations = new() { ["summary"] = "B summary" },
            StartsAt    = DateTimeOffset.UtcNow,
        };

        var payload = new AlertmanagerWebhookPayload
        {
            Version = "4", Status = "firing", Receiver = "webhook",
            GroupLabels = new(), CommonLabels = new(), CommonAnnotations = new(),
            Alerts = new[] { alert1, alert2 },
        };

        ai.TriageIncidentAsync(Arg.Any<IncidentTriageAiRequest>(), Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        var results = await svc.ProcessWebhookAsync(payload);

        Assert.Equal(2, results.Count);
        await ai.Received(2)
                .TriageIncidentAsync(Arg.Any<IncidentTriageAiRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWebhookAsync_SecondAlertFails_FirstStillReturned()
    {
        var (svc, ai) = CreateSut();

        var alert1 = new AlertmanagerAlert
        {
            Status      = "firing",
            Labels      = new() { ["alertname"] = "GoodAlert", ["job"] = "api" },
            Annotations = new() { ["summary"] = "Good" },
            StartsAt    = DateTimeOffset.UtcNow,
        };
        var alert2 = new AlertmanagerAlert
        {
            Status      = "firing",
            Labels      = new() { ["alertname"] = "BadAlert", ["job"] = "api" },
            Annotations = new() { ["summary"] = "Bad" },
            StartsAt    = DateTimeOffset.UtcNow,
        };

        var payload = new AlertmanagerWebhookPayload
        {
            Version = "4", Status = "firing", Receiver = "webhook",
            GroupLabels = new(), CommonLabels = new(), CommonAnnotations = new(),
            Alerts = new[] { alert1, alert2 },
        };

        ai.TriageIncidentAsync(
                Arg.Is<IncidentTriageAiRequest>(r => r.AlertName == "GoodAlert"),
                Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        ai.TriageIncidentAsync(
                Arg.Is<IncidentTriageAiRequest>(r => r.AlertName == "BadAlert"),
                Arg.Any<CancellationToken>())
          .Returns(Task.FromException<IncidentTriageAiResult>(
              new AiUnavailableException("provider down")));

        var results = await svc.ProcessWebhookAsync(payload);

        Assert.Single(results);
        Assert.Equal("GoodAlert", results[0].AlertName);
    }

    // ── Label redaction ───────────────────────────────────────────────────

    [Theory]
    [InlineData("password",         "s3cr3t")]
    [InlineData("db_password",      "mysecret")]
    [InlineData("apikey",           "sk-abc123")]
    [InlineData("api_key",          "sk-abc123")]
    [InlineData("token",            "eyJhb...")]
    [InlineData("secret",           "topsecret")]
    [InlineData("connectionstring", "Server=db;Uid=root;Pwd=pass")]
    public async Task ProcessWebhookAsync_SensitiveKeyLabel_IsRedacted(
        string labelKey, string labelValue)
    {
        var (svc, ai) = CreateSut();

        var payload = BuildPayload(extraLabels: new() { [labelKey] = labelValue });

        IncidentTriageAiRequest? captured = null;
        ai.TriageIncidentAsync(
                Arg.Do<IncidentTriageAiRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        await svc.ProcessWebhookAsync(payload);

        Assert.NotNull(captured);
        var sanitisedLabel = captured!.Labels.FirstOrDefault(kv => kv.Key == labelKey);
        Assert.Equal("[REDACTED]", sanitisedLabel.Value);
    }

    [Fact]
    public async Task ProcessWebhookAsync_SensitiveValuePattern_IsRedacted()
    {
        var (svc, ai) = CreateSut();

        // A long value that contains a connection-string fragment
        var connString = "Server=postgres;Database=dineos;User Id=app;Password=hunter2;";
        var payload = BuildPayload(
            extraLabels: new() { ["db_dsn"] = connString });

        IncidentTriageAiRequest? captured = null;
        ai.TriageIncidentAsync(
                Arg.Do<IncidentTriageAiRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        await svc.ProcessWebhookAsync(payload);

        Assert.NotNull(captured);
        var label = captured!.Labels.FirstOrDefault(kv => kv.Key == "db_dsn");
        Assert.Equal("[REDACTED]", label.Value);
    }

    [Fact]
    public async Task ProcessWebhookAsync_SafeLabel_IsNotRedacted()
    {
        var (svc, ai) = CreateSut();

        var payload = BuildPayload(
            extraLabels: new() { ["env"] = "production", ["region"] = "eu-west-1" });

        IncidentTriageAiRequest? captured = null;
        ai.TriageIncidentAsync(
                Arg.Do<IncidentTriageAiRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
          .Returns(BuildTriageResult());

        await svc.ProcessWebhookAsync(payload);

        Assert.NotNull(captured);
        Assert.Contains(captured!.Labels, kv => kv.Key == "env" && kv.Value == "production");
        Assert.Contains(captured.Labels,  kv => kv.Key == "region" && kv.Value == "eu-west-1");
    }
}
