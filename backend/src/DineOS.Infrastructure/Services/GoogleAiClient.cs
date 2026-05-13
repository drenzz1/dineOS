using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public sealed class GoogleAiClient(
    HttpClient http,
    IOptions<GoogleAiOptions> options,
    ILogger<GoogleAiClient> logger) : IAiClient
{
    public const string HttpClientName = "google-ai";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly GoogleAiOptions _opts = options.Value;

    public async Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new AiUnavailableException("Google AI API key is not configured (GoogleAI:ApiKey).");

        var body = new GenerateContentRequest(
            Contents:
            [
                new Content("user", [new Part(BuildUserMessage(request))]),
            ],
            SystemInstruction: new SystemInstruction([new Part(SystemPrompt)]),
            GenerationConfig: new GenerationConfig(_opts.MaxTokens, "application/json"));

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(BuildGenerateContentPath(), body, JsonOpts, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Google AI call timed out after {Timeout}s. Item={Name}", _opts.TimeoutSeconds, request.Name);
            throw new AiUnavailableException("Google AI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google AI network error. Item={Name}", request.Name);
            throw new AiUnavailableException("Google AI network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google AI returned {StatusCode}. Snippet={Snippet}", (int)response.StatusCode, Truncate(snippet, 400));
            throw new AiUnavailableException($"Google AI returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("Google AI response was empty.");
        var content = payload.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
                      ?? throw new AiUnavailableException("Google AI response did not include content.");

        var parsed = OpenAiClient.ParseSuggestion(content, "Google AI");
        return new MenuDescriptionAiResult(
            parsed.Description,
            parsed.Allergens,
            new AiUsage(
                payload.UsageMetadata?.PromptTokenCount ?? 0,
                payload.UsageMetadata?.CandidatesTokenCount ?? 0,
                _opts.Model));
    }

    private string BuildGenerateContentPath()
    {
        var version = _opts.ApiVersion.Trim('/');
        var model = _opts.Model.StartsWith("models/", StringComparison.Ordinal)
            ? _opts.Model
            : $"models/{_opts.Model}";

        return $"/{version}/{model}:generateContent";
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

    private sealed record GenerateContentRequest(
        Content[] Contents,
        [property: JsonPropertyName("system_instruction")] SystemInstruction SystemInstruction,
        [property: JsonPropertyName("generation_config")] GenerationConfig GenerationConfig);

    private sealed record Content(string Role, Part[] Parts);

    private sealed record Part(string Text);

    private sealed record SystemInstruction(Part[] Parts);

    private sealed record GenerationConfig(
        [property: JsonPropertyName("max_output_tokens")] int MaxOutputTokens,
        [property: JsonPropertyName("response_mime_type")] string ResponseMimeType);

    private sealed record GenerateContentResponse(
        Candidate[]? Candidates,
        [property: JsonPropertyName("usage_metadata")] UsageMetadata? UsageMetadata);

    private sealed record Candidate(ContentResponse? Content);

    private sealed record ContentResponse(PartResponse[]? Parts);

    private sealed record PartResponse(string? Text);

    private sealed record UsageMetadata(
        [property: JsonPropertyName("prompt_token_count")] int PromptTokenCount,
        [property: JsonPropertyName("candidates_token_count")] int CandidatesTokenCount);
}
