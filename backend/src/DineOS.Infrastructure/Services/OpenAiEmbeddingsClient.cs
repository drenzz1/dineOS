using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public sealed class OpenAiEmbeddingsClient(
    HttpClient http,
    ILogger<OpenAiEmbeddingsClient> logger,
    string apiKey) : IEmbeddingsClient
{
    public const string HttpClientName = "openai-embeddings";
    private const string Model = "text-embedding-3-small";
    private const int Dimensions = 768;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiUnavailableException("OpenAI embeddings API key is not configured. Visit Admin → Settings to add one.");

        var body = new EmbeddingRequest(Model, text.Trim(), Dimensions);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/embeddings")
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenAI embeddings call timed out.");
            throw new AiUnavailableException("OpenAI embeddings request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OpenAI embeddings network error.");
            throw new AiUnavailableException("OpenAI embeddings network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("OpenAI embeddings returned {Status}. Snippet={Snippet}", (int)response.StatusCode, snippet.Length > 300 ? snippet[..300] : snippet);
            throw new AiUnavailableException($"OpenAI embeddings returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("OpenAI embeddings response was empty.");

        var values = payload.Data?.FirstOrDefault()?.Embedding
                     ?? throw new AiUnavailableException("OpenAI embeddings response did not include a vector.");

        return values;
    }

    private sealed record EmbeddingRequest(string Model, string Input, int Dimensions);

    private sealed record EmbeddingResponse(EmbeddingData[]? Data);

    private sealed record EmbeddingData(float[]? Embedding);
}
