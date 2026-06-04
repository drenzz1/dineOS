using System.Net;
using System.Text;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DineOS.Tests.Unit;

public class OpenAiClientTests
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
                      "model": "gpt-test",
                      "choices": [
                        { "message": { "content": "{\"description\":\"Crisp fries with sea salt.\",\"allergens\":[]}" } }
                      ],
                      "usage": { "prompt_tokens": 11, "completion_tokens": 6 }
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task GenerateMenuDescriptionAsync_UsesConfiguredModelAndBearerToken()
    {
        var options = new OpenAiOptions { ApiKey = "openai-key", Model = "gpt-test" };
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        var client = new OpenAiClient(http, Options.Create(options), NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateMenuDescriptionAsync(
            new MenuDescriptionAiRequest("Fries", "Sides", 4m, null));

        Assert.Equal("gpt-test", result.Usage.Model);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("openai-key", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"model\":\"gpt-test\"", handler.LastBody);
        Assert.Contains("\"response_format\"", handler.LastBody);
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
                      "model": "gpt-triage-test",
                      "choices": [
                        { "message": { "content": "{\"severity\":\"high\",\"likely_causes\":[\"Redis OOM\",\"Connection pool exhausted\"],\"suggested_next_actions\":[\"Restart Redis\",\"Check pod logs\"],\"short_summary\":\"Redis is down causing order write failures.\"}" } }
                      ],
                      "usage": { "prompt_tokens": 200, "completion_tokens": 80 }
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task TriageIncidentAsync_ParsesJsonResponse()
    {
        var options = new OpenAiOptions { ApiKey = "openai-key", Model = "gpt-triage-test" };
        var handler = new TriageStubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        var client = new OpenAiClient(http, Options.Create(options), NullLogger<OpenAiClient>.Instance);

        var result = await client.TriageIncidentAsync(new IncidentTriageAiRequest(
            AlertName:   "RedisDown",
            Severity:    "critical",
            Component:   "cache",
            Status:      "firing",
            Summary:     "Redis unreachable",
            Description: "All SET/GET operations returning ECONNREFUSED",
            Labels:      [new KeyValuePair<string, string>("env", "prod")],
            FiringSince: new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal("high", result.Severity);
        Assert.Equal(2, result.LikelyCauses.Count);
        Assert.Contains("Redis OOM", result.LikelyCauses);
        Assert.Equal(2, result.SuggestedNextActions.Count);
        Assert.Contains("Restart Redis", result.SuggestedNextActions);
        Assert.Contains("Redis", result.ShortSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(200, result.Usage.InputTokens);
        Assert.Equal(80,  result.Usage.OutputTokens);
        Assert.Equal("gpt-triage-test", result.Usage.Model);

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("openai-key", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"response_format\"", handler.LastBody);
    }
}
