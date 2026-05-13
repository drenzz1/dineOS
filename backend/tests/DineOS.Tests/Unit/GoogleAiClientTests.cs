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
}
