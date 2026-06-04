using System.Net;
using System.Text.Json;
using DineOS.Application.Alerts;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DineOS.Tests.Unit;

public class SlackNotifierTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Responder?.Invoke(request)
                ?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }
    }

    private static (SlackNotifier notifier, StubHandler handler) CreateSut(string webhookUrl = "https://hooks.slack.com/test")
    {
        var opts    = new SlackOptions { WebhookUrl = webhookUrl, TimeoutSeconds = 10 };
        var handler = new StubHandler();
        var http    = new HttpClient(handler);

        var notifier = new SlackNotifier(
            http,
            Options.Create(opts),
            NullLogger<SlackNotifier>.Instance);

        return (notifier, handler);
    }

    private static IncidentTriageResultDto SampleResult() => new(
        CorrelationId:        "abc123",
        AlertName:            "HighErrorRate",
        Severity:             "critical",
        LikelyCauses:         new[] { "Redis OOM", "Connection pool exhausted" },
        SuggestedNextActions: new[] { "Restart Redis", "Check pod logs" },
        ShortSummary:         "Redis is down causing order write failures.",
        Usage:                new AiUsage(200, 80, "claude-sonnet-test"));

    // ── 1. Happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyTriageAsync_PostsToWebhookUrl()
    {
        var (notifier, handler) = CreateSut();

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://hooks.slack.com/test", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task NotifyTriageAsync_BodyContainsAlertNameAndSummary()
    {
        var (notifier, handler) = CreateSut();

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.NotNull(handler.LastBody);
        Assert.Contains("HighErrorRate", handler.LastBody);
        Assert.Contains("Redis is down", handler.LastBody);
    }

    [Fact]
    public async Task NotifyTriageAsync_BodyContainsCorrelationId()
    {
        var (notifier, handler) = CreateSut();

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.NotNull(handler.LastBody);
        Assert.Contains("abc123", handler.LastBody);
    }

    [Fact]
    public async Task NotifyTriageAsync_BodyContainsLikelyCausesAndNextActions()
    {
        var (notifier, handler) = CreateSut();

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.Contains("Redis OOM",     handler.LastBody);
        Assert.Contains("Restart Redis", handler.LastBody);
    }

    // ── 2. No-op when WebhookUrl is empty ─────────────────────────────────

    [Fact]
    public async Task NotifyTriageAsync_EmptyWebhookUrl_DoesNotPost()
    {
        var (notifier, handler) = CreateSut(webhookUrl: "");

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task NotifyTriageAsync_WhitespaceWebhookUrl_DoesNotPost()
    {
        var (notifier, handler) = CreateSut(webhookUrl: "   ");

        await notifier.NotifyTriageAsync(SampleResult());

        Assert.Null(handler.LastRequest);
    }

    // ── 3. HTTP failure → logged, does not throw ──────────────────────────

    [Fact]
    public async Task NotifyTriageAsync_SlackReturnsError_DoesNotThrow()
    {
        var (notifier, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("no_service"),
        };

        var ex = await Record.ExceptionAsync(() => notifier.NotifyTriageAsync(SampleResult()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task NotifyTriageAsync_HttpClientThrows_DoesNotThrow()
    {
        var (notifier, handler) = CreateSut();
        handler.Responder = _ => throw new HttpRequestException("network error");

        var ex = await Record.ExceptionAsync(() => notifier.NotifyTriageAsync(SampleResult()));

        Assert.Null(ex);
    }

    // ── 4. Payload shape ──────────────────────────────────────────────────

    [Fact]
    public async Task NotifyTriageAsync_PayloadHasBlocksArray()
    {
        var (notifier, handler) = CreateSut();

        await notifier.NotifyTriageAsync(SampleResult());

        var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.True(doc.RootElement.TryGetProperty("blocks", out var blocks));
        Assert.Equal(JsonValueKind.Array, blocks.ValueKind);
        Assert.True(blocks.GetArrayLength() > 0);
    }
}
