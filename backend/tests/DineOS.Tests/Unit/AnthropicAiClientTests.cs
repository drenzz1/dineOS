using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DineOS.Tests.Unit;

public class AnthropicAiClientTests
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
                ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }

    private static (AnthropicAiClient client, StubHandler handler) CreateSut(AnthropicOptions? options = null)
    {
        var opts = options ?? new AnthropicOptions { ApiKey = "test-key" };
        var handler = new StubHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(opts.BaseUrl),
            Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds),
        };
        http.DefaultRequestHeaders.Add("anthropic-version", opts.ApiVersion);
        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            http.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);

        var client = new AnthropicAiClient(http, Options.Create(opts), NullLogger<AnthropicAiClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_ParsesToolUseResponse()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "content": [
                    {
                      "type": "tool_use",
                      "name": "report_menu_description",
                      "input": {
                        "description": "Wood-fired pizza with tomato, mozzarella, and basil.",
                        "allergens": ["gluten", "dairy"]
                      }
                    }
                  ],
                  "usage": { "input_tokens": 130, "output_tokens": 42 }
                }
                """, Encoding.UTF8, "application/json"),
        };

        var result = await client.GenerateMenuDescriptionAsync(
            new MenuDescriptionAiRequest("Margherita", "Pizza", 9.5m, null));

        Assert.Contains("tomato", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "gluten", "dairy" }, result.Allergens);
        Assert.Equal(130, result.Usage.InputTokens);
        Assert.Equal(42,  result.Usage.OutputTokens);

        // Verify wire shape includes the tool definition and forces tool use.
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"tool_choice\"", handler.LastBody);
        Assert.Contains("\"report_menu_description\"", handler.LastBody);
        Assert.Contains("\"max_tokens\"", handler.LastBody);
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_UsesConfiguredModelAndApiKey()
    {
        var (client, handler) = CreateSut(new AnthropicOptions
        {
            ApiKey = "configured-key",
            Model = "claude-future-model",
        });
        handler.Responder = request =>
        {
            Assert.True(request.Headers.TryGetValues("x-api-key", out var values));
            Assert.Equal("configured-key", Assert.Single(values));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "content": [
                        { "type": "tool_use", "name": "report_menu_description",
                          "input": { "description": "Bright citrus salad with fresh herbs.", "allergens": [] } }
                      ],
                      "usage": { "input_tokens": 10, "output_tokens": 5 }
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        };

        var result = await client.GenerateMenuDescriptionAsync(
            new MenuDescriptionAiRequest("Citrus Salad", "Salads", 8m, null));

        Assert.Equal("claude-future-model", result.Usage.Model);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"model\":\"claude-future-model\"", handler.LastBody);
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_MissingApiKey_ThrowsAiUnavailable()
    {
        var (client, _) = CreateSut(new AnthropicOptions { ApiKey = "" });

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.GenerateMenuDescriptionAsync(
                new MenuDescriptionAiRequest("X", "Y", 1m, null)));
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_HttpError_ThrowsAiUnavailable()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream broken"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.GenerateMenuDescriptionAsync(
                new MenuDescriptionAiRequest("X", "Y", 1m, null)));
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_EmptyDescription_ThrowsAiUnavailable()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "content": [
                    { "type": "tool_use", "name": "report_menu_description",
                      "input": { "description": "", "allergens": [] } }
                  ],
                  "usage": { "input_tokens": 1, "output_tokens": 1 }
                }
                """, Encoding.UTF8, "application/json"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.GenerateMenuDescriptionAsync(
                new MenuDescriptionAiRequest("X", "Y", 1m, null)));
    }

    // ── TriageIncidentAsync ───────────────────────────────────────────────

    private static IncidentTriageAiRequest SampleTriageRequest() => new(
        AlertName:   "HighErrorRate",
        Severity:    "critical",
        Component:   "order-api",
        Status:      "firing",
        Summary:     "Error rate above 10% for 5 minutes",
        Description: "HTTP 500 responses spiking on /api/orders",
        Labels:      [new KeyValuePair<string, string>("env", "prod"), new KeyValuePair<string, string>("namespace", "project-06")],
        FiringSince: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task TriageIncidentAsync_ParsesToolUseResponse()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "content": [
                    {
                      "type": "tool_use",
                      "name": "report_incident_triage",
                      "input": {
                        "severity": "high",
                        "likely_causes": ["Redis connection pool exhausted", "Memory pressure on order-service"],
                        "suggested_next_actions": ["Restart Redis replica", "Check order-service memory metrics"],
                        "short_summary": "Redis is unreachable causing order writes to fail."
                      }
                    }
                  ],
                  "usage": { "input_tokens": 200, "output_tokens": 80 }
                }
                """, Encoding.UTF8, "application/json"),
        };

        var result = await client.TriageIncidentAsync(SampleTriageRequest());

        Assert.Equal("high", result.Severity);
        Assert.Equal(2, result.LikelyCauses.Count);
        Assert.Contains("Redis connection pool exhausted", result.LikelyCauses);
        Assert.Equal(2, result.SuggestedNextActions.Count);
        Assert.Contains("Restart Redis replica", result.SuggestedNextActions);
        Assert.Contains("Redis", result.ShortSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(200, result.Usage.InputTokens);
        Assert.Equal(80,  result.Usage.OutputTokens);

        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"report_incident_triage\"", handler.LastBody);
        Assert.Contains("\"tool_choice\"",            handler.LastBody);
        Assert.Contains("\"max_tokens\"",             handler.LastBody);
    }

    [Fact]
    public async Task TriageIncidentAsync_MissingApiKey_ThrowsAiUnavailable()
    {
        var (client, _) = CreateSut(new AnthropicOptions { ApiKey = "" });

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.TriageIncidentAsync(SampleTriageRequest()));
    }

    [Fact]
    public async Task TriageIncidentAsync_HttpError_ThrowsAiUnavailable()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream error"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.TriageIncidentAsync(SampleTriageRequest()));
    }

    [Fact]
    public async Task TriageIncidentAsync_EmptyShortSummary_ThrowsAiUnavailable()
    {
        var (client, handler) = CreateSut();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "content": [
                    {
                      "type": "tool_use",
                      "name": "report_incident_triage",
                      "input": {
                        "severity": "high",
                        "likely_causes": ["OOM"],
                        "suggested_next_actions": ["Restart pod"],
                        "short_summary": ""
                      }
                    }
                  ],
                  "usage": { "input_tokens": 1, "output_tokens": 1 }
                }
                """, Encoding.UTF8, "application/json"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            client.TriageIncidentAsync(SampleTriageRequest()));
    }
}
