using DineOS.Application.Alerts;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/v1/alerts/webhook.
/// Verifies the "never block the alert pipeline" contract:
///   - Successful triage → 200 with results
///   - LLM unavailable → 200 with empty results
///   - Service throws → 200 with empty results (controller outer catch)
///   - Wrong shared secret → 200 (dropped but acknowledged)
///   - No secret configured → anonymous access accepted
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class AlertsWebhookIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string WebhookPath = "/api/v1/alerts/webhook";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Helpers ───────────────────────────────────────────────────────────

    private static StringContent BuildPayloadContent(
        string alertName = "HighErrorRate",
        string severity  = "critical",
        string status    = "firing") =>
        new(JsonSerializer.Serialize(new
        {
            version   = "4",
            groupKey  = "{}:{alertname=\"HighErrorRate\"}",
            status,
            receiver  = "dineos-webhook",
            groupLabels       = new { alertname = alertName },
            commonLabels      = new { alertname = alertName, severity },
            commonAnnotations = new { summary = "Error rate is above 10%", description = "HTTP 500 spike" },
            externalURL       = "http://alertmanager:9093",
            alerts = new[]
            {
                new
                {
                    status,
                    labels = new { alertname = alertName, severity, job = "dineos-api" },
                    annotations = new { summary = "Error rate above 10% for 5 min", description = "HTTP 500 spike" },
                    startsAt    = "2026-06-04T12:00:00.000Z",
                    endsAt      = "0001-01-01T00:00:00.000Z",
                    generatorURL = "http://prometheus:9090/graph",
                    fingerprint  = "abc123def456",
                }
            }
        }), Encoding.UTF8, "application/json");

    private HttpClient CreateClientWithTriageStub(IIncidentTriageService stub) =>
        factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s =>
            {
                var d = s.SingleOrDefault(x => x.ServiceType == typeof(IIncidentTriageService));
                if (d is not null) s.Remove(d);
                s.AddScoped<IIncidentTriageService>(_ => stub);
            }))
        .CreateClient();

    private HttpClient CreateClientWithSecret(string secret, IIncidentTriageService? stub = null) =>
        factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AlertWebhook:SharedSecret"] = secret,
                }));
            b.ConfigureServices(s =>
            {
                var d = s.SingleOrDefault(x => x.ServiceType == typeof(IIncidentTriageService));
                if (d is not null) s.Remove(d);
                s.AddScoped<IIncidentTriageService>(_ =>
                    stub ?? new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
                        Array.Empty<IncidentTriageResultDto>())));
            });
        })
        .CreateClient();

    // ── 1. Happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidWebhook_Returns200_WithTriageResults()
    {
        var expectedResult = new IncidentTriageResultDto(
            CorrelationId:        "abc",
            AlertName:            "HighErrorRate",
            Severity:             "high",
            LikelyCauses:         new[] { "Redis OOM" },
            SuggestedNextActions: new[] { "Restart Redis" },
            ShortSummary:         "Redis is down.",
            Usage:                new AiUsage(200, 80, "claude-sonnet-test"));

        var stub   = new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
            new[] { expectedResult }));
        var client = CreateClientWithTriageStub(stub);

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(JsonValueKind.Array, envelope.Data.ValueKind);
        Assert.Equal(1, envelope.Data.GetArrayLength());
    }

    // ── 2. LLM unavailable → 200 with empty results ───────────────────────

    [Fact]
    public async Task Post_Webhook_LlmFails_Returns200_WithEmptyResults()
    {
        var stub = new StubTriageService(
            Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
                Array.Empty<IncidentTriageResultDto>()));
        var client = CreateClientWithTriageStub(stub);

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(0, envelope.Data.GetArrayLength());
    }

    // ── 3. Service throws → controller outer catch → 200 ─────────────────

    [Fact]
    public async Task Post_Webhook_ServiceThrows_Returns200_AlertPipelineNeverBlocked()
    {
        var stub = new ThrowingTriageService(
            new AiUnavailableException("AI provider completely down"));
        var client = CreateClientWithTriageStub(stub);

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(0, envelope.Data.GetArrayLength());
    }

    [Fact]
    public async Task Post_Webhook_ServiceThrowsUnexpected_Returns200()
    {
        var stub = new ThrowingTriageService(
            new InvalidOperationException("catastrophic failure"));
        var client = CreateClientWithTriageStub(stub);

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 4. Shared-secret validation ───────────────────────────────────────

    [Fact]
    public async Task Post_Webhook_CorrectSecret_Returns200_WithResults()
    {
        var stub = new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
            Array.Empty<IncidentTriageResultDto>()));
        var client = CreateClientWithSecret("my-test-secret", stub);
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", "my-test-secret");

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
    }

    [Fact]
    public async Task Post_Webhook_CorrectSecret_ViaBearer_Returns200()
    {
        // Alertmanager sets "Authorization: Bearer <secret>" via http_config.authorization.
        var stub = new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
            Array.Empty<IncidentTriageResultDto>()));
        var client = CreateClientWithSecret("alertmanager-secret", stub);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "alertmanager-secret");

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
    }

    [Fact]
    public async Task Post_Webhook_WrongSecret_Returns200_PayloadDropped()
    {
        var client = CreateClientWithSecret("expected-secret");
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", "wrong-secret");

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        // mismatch path returns empty results + a descriptive message
        Assert.Equal(0, envelope.Data.GetArrayLength());
        Assert.NotNull(envelope.Message);
        Assert.Contains("mismatch", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Webhook_MissingSecretHeader_Returns200_PayloadDropped()
    {
        var client = CreateClientWithSecret("required-secret");
        // intentionally no X-Webhook-Secret header

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("mismatch", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── 5. No secret configured → anonymous allowed ───────────────────────

    [Fact]
    public async Task Post_Webhook_NoSecretConfigured_AnonymousAllowed()
    {
        var stub = new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
            Array.Empty<IncidentTriageResultDto>()));
        // factory has no AlertWebhook:SharedSecret configured by default
        var client = CreateClientWithTriageStub(stub);
        // no Authorization header, no X-Webhook-Secret header

        var response = await client.PostAsync(WebhookPath, BuildPayloadContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 6. Empty alerts array ─────────────────────────────────────────────

    [Fact]
    public async Task Post_Webhook_EmptyAlerts_Returns200_WithEmptyResults()
    {
        var stub = new StubTriageService(Task.FromResult<IReadOnlyList<IncidentTriageResultDto>>(
            Array.Empty<IncidentTriageResultDto>()));
        var client = CreateClientWithTriageStub(stub);

        var emptyPayload = new StringContent(
            JsonSerializer.Serialize(new
            {
                version = "4", status = "firing", receiver = "webhook",
                groupLabels = new { }, commonLabels = new { }, commonAnnotations = new { },
                alerts = Array.Empty<object>(),
            }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync(WebhookPath, emptyPayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body     = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOpts);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.Equal(0, envelope.Data.GetArrayLength());
    }

    // ── Stub helpers ──────────────────────────────────────────────────────

    private sealed class StubTriageService(
        Task<IReadOnlyList<IncidentTriageResultDto>> result) : IIncidentTriageService
    {
        public Task<IReadOnlyList<IncidentTriageResultDto>> ProcessWebhookAsync(
            AlertmanagerWebhookPayload payload, CancellationToken ct) => result;
    }

    private sealed class ThrowingTriageService(Exception ex) : IIncidentTriageService
    {
        public Task<IReadOnlyList<IncidentTriageResultDto>> ProcessWebhookAsync(
            AlertmanagerWebhookPayload payload, CancellationToken ct) =>
            Task.FromException<IReadOnlyList<IncidentTriageResultDto>>(ex);
    }
}
