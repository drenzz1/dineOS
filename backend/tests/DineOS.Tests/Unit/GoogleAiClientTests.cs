using System.Net;
using System.Text;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DineOS.Tests.Unit;

public class GoogleAiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              { "text": "{\"description\":\"Creamy yogurt with honey.\",\"allergens\":[\"dairy\"]}" }
                            ]
                          }
                        }
                      ],
                      "usage_metadata": { "prompt_token_count": 12, "candidates_token_count": 7 }
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_UsesConfiguredModelAndApiKey()
    {
        var options = new GoogleAiOptions { ApiKey = "google-key", Model = "gemini-test" };
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Add("x-goog-api-key", options.ApiKey);
        var client = new GoogleAiClient(http, Options.Create(options), NullLogger<GoogleAiClient>.Instance);

        var result = await client.GenerateMenuDescriptionAsync(
            new MenuDescriptionAiRequest("Yogurt", "Breakfast", 5m, null));

        Assert.Equal("gemini-test", result.Usage.Model);
        Assert.Equal(new[] { "dairy" }, result.Allergens);
        Assert.Equal("/v1beta/models/gemini-test:generateContent", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-goog-api-key", out var values));
        Assert.Equal("google-key", Assert.Single(values));
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"response_mime_type\":\"application/json\"", handler.LastBody);
    }

    // ── TriageIncidentAsync ───────────────────────────────────────────────

    private sealed class TriageStubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              { "text": "{\"severity\":\"high\",\"likely_causes\":[\"Redis OOM\",\"Eviction policy too aggressive\"],\"suggested_next_actions\":[\"Increase maxmemory\",\"Check eviction logs\"],\"short_summary\":\"Redis is OOM causing cache misses and write failures.\"}" }
                            ]
                          }
                        }
                      ],
                      "usage_metadata": { "prompt_token_count": 200, "candidates_token_count": 80 }
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task TriageIncidentAsync_ParsesJsonResponse()
    {
        var options = new GoogleAiOptions { ApiKey = "google-key", Model = "gemini-triage-test" };
        var handler = new TriageStubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Add("x-goog-api-key", options.ApiKey);
        var client = new GoogleAiClient(http, Options.Create(options), NullLogger<GoogleAiClient>.Instance);

        var result = await client.TriageIncidentAsync(new IncidentTriageAiRequest(
            AlertName:   "RedisOOM",
            Severity:    "critical",
            Component:   "cache",
            Status:      "firing",
            Summary:     "Redis OOM killer triggered",
            Description: "maxmemory-policy=allkeys-lru, memory usage at 100%",
            Labels:      [new KeyValuePair<string, string>("env", "prod")],
            FiringSince: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal("high", result.Severity);
        Assert.Equal(2, result.LikelyCauses.Count);
        Assert.Contains("Redis OOM", result.LikelyCauses);
        Assert.Equal(2, result.SuggestedNextActions.Count);
        Assert.Contains("Increase maxmemory", result.SuggestedNextActions);
        Assert.Contains("Redis", result.ShortSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(200, result.Usage.InputTokens);
        Assert.Equal(80,  result.Usage.OutputTokens);
        Assert.Equal("gemini-triage-test", result.Usage.Model);

        Assert.Equal("/v1beta/models/gemini-triage-test:generateContent", handler.LastRequest?.RequestUri?.PathAndQuery);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-goog-api-key", out var vals));
        Assert.Equal("google-key", Assert.Single(vals));
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"response_mime_type\":\"application/json\"", handler.LastBody);
    }
}
