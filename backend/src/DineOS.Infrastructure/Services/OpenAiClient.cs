using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public sealed class OpenAiClient(
    HttpClient http,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiClient> logger) : IAiClient
{
    public const string HttpClientName = "openai";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OpenAiOptions _opts = options.Value;

    public async Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new AiUnavailableException("OpenAI API key is not configured (OpenAI:ApiKey).");

        var body = new ChatCompletionRequest(
            Model: _opts.Model,
            MaxTokens: _opts.MaxTokens,
            ResponseFormat: new ResponseFormat("json_object"),
            Messages:
            [
                new Message("system", SystemPrompt),
                new Message("user", BuildUserMessage(request)),
            ]);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/v1/chat/completions", body, JsonOpts, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenAI call timed out after {Timeout}s. Item={Name}", _opts.TimeoutSeconds, request.Name);
            throw new AiUnavailableException("OpenAI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OpenAI network error. Item={Name}", request.Name);
            throw new AiUnavailableException("OpenAI network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("OpenAI returned {StatusCode}. Snippet={Snippet}", (int)response.StatusCode, Truncate(snippet, 400));
            throw new AiUnavailableException($"OpenAI returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("OpenAI response was empty.");
        var content = payload.Choices?.FirstOrDefault()?.Message?.Content
                      ?? throw new AiUnavailableException("OpenAI response did not include content.");

        var parsed = ParseSuggestion(content, "OpenAI");
        return new MenuDescriptionAiResult(
            parsed.Description,
            parsed.Allergens,
            new AiUsage(
                payload.Usage?.PromptTokens ?? 0,
                payload.Usage?.CompletionTokens ?? 0,
                payload.Model ?? _opts.Model));
    }

    internal static (string Description, IReadOnlyList<string> Allergens) ParseSuggestion(string content, string providerName)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var description = root.TryGetProperty("description", out var d)
                ? d.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(description))
                throw new AiUnavailableException($"{providerName} returned an empty description.");

            var allergens = new List<string>();
            if (root.TryGetProperty("allergens", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var allergen = el.GetString();
                    if (!string.IsNullOrWhiteSpace(allergen))
                        allergens.Add(allergen);
                }
            }

            return (description.Trim(), allergens);
        }
        catch (JsonException ex)
        {
            throw new AiUnavailableException($"{providerName} returned invalid JSON.", ex);
        }
    }

    private const string SystemPrompt = """
        You are a helpful assistant for a restaurant POS. Generate concise,
        appetizing menu copy. Return only JSON with this shape:
        {"description":"...","allergens":["..."]}

        Rules:
        - description: 1-2 sentences, max 200 characters, no marketing fluff,
          present tense, customer-facing.
        - allergens: only entries that are clearly implied by the dish (e.g.
          "gluten", "dairy", "shellfish", "nuts", "soy", "egg"). Empty array
          if nothing is clearly implied. Do not invent allergens.
        """;

    private static string BuildUserMessage(MenuDescriptionAiRequest r)
    {
        var existing = string.IsNullOrWhiteSpace(r.ExistingDescription)
            ? "(none)"
            : Truncate(r.ExistingDescription, 200);

        return $"""
                Menu item:
                - Name: {r.Name}
                - Category: {r.Category}
                - Price: {r.Price:F2}
                - Current description: {existing}
                """;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed record ChatCompletionRequest(
        string Model,
        Message[] Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private sealed record Message(string Role, string Content);

    private sealed record ResponseFormat(string Type);

    private sealed record ChatCompletionResponse(
        string? Model,
        Choice[]? Choices,
        Usage? Usage);

    private sealed record Choice(MessageContent? Message);

    private sealed record MessageContent(string? Content);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
