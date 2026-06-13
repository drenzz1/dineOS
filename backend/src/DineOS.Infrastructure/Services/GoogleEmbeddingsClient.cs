using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public sealed class GoogleEmbeddingsClient(
    HttpClient http,
    ILogger<GoogleEmbeddingsClient> logger,
    string apiKey) : IEmbeddingsClient
{
    public const string HttpClientName = "google-embeddings";
    private const string Model = "text-embedding-004";
    private const int Dimensions = 768;
    private const string ApiVersion = "v1";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiUnavailableException("Google embeddings API key is not configured. Visit Admin → Settings to add one.");

        var body = new EmbedContentRequest(
            new EmbedContent(text.Trim()),
            OutputDimensionality: Dimensions);

        var path = $"/{ApiVersion}/models/{Model}:embedContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Google embeddings call timed out.");
            throw new AiUnavailableException("Google embeddings request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google embeddings network error.");
            throw new AiUnavailableException("Google embeddings network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google embeddings returned {Status}. Snippet={Snippet}", (int)response.StatusCode, snippet.Length > 300 ? snippet[..300] : snippet);
            throw new AiUnavailableException($"Google embeddings returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbedContentResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("Google embeddings response was empty.");

        var values = payload.Embedding?.Values
                     ?? throw new AiUnavailableException("Google embeddings response did not include a vector.");

        return values;
    }

    private sealed record EmbedContentRequest(
        EmbedContent Content,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);

    private sealed record EmbedContent(string Text);

    private sealed record EmbedContentResponse(EmbeddingValues? Embedding);

    private sealed record EmbeddingValues(float[]? Values);
}
