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
    ILogger<GoogleAiClient> logger,
    string? apiKeyOverride = null) : IAiClient
{
    public const string HttpClientName = "google-ai";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly GoogleAiOptions _opts = options.Value;

    private string EffectiveApiKey =>
        !string.IsNullOrWhiteSpace(apiKeyOverride) ? apiKeyOverride : _opts.ApiKey;

    public async Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("Google AI API key is not configured. Visit Admin → Settings to add one.");

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
            response = await SendAsync(key, BuildGenerateContentPath(), body, ct);
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

    public async Task<IncidentTriageAiResult> TriageIncidentAsync(
        IncidentTriageAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("Google AI API key is not configured. Visit Admin → Settings to add one.");

        var body = new GenerateContentRequest(
            Contents:
            [
                new Content("user", [new Part(BuildTriageUserMessage(request))]),
            ],
            SystemInstruction: new SystemInstruction([new Part(TriageSystemPrompt)]),
            GenerationConfig: new GenerationConfig(_opts.MaxTokens, "application/json"));

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(key, BuildGenerateContentPath(), body, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Google AI triage call timed out after {Timeout}s. Alert={AlertName}", _opts.TimeoutSeconds, request.AlertName);
            throw new AiUnavailableException("Google AI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google AI triage network error. Alert={AlertName}", request.AlertName);
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

        var parsed = OpenAiClient.ParseTriage(content, "Google AI");
        return new IncidentTriageAiResult(
            parsed.Severity,
            parsed.LikelyCauses,
            parsed.SuggestedNextActions,
            parsed.ShortSummary,
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

    private const string TriageSystemPrompt = """
        You are an SRE incident-triage assistant. Analyse the incoming alert and return
        ONLY JSON with this shape:
        {"severity":"...","likely_causes":["..."],"suggested_next_actions":["..."],"short_summary":"..."}

        Rules:
        - severity: re-assess as critical|high|medium|low based on context.
        - likely_causes: 1–5 concise probable root causes, no duplication.
        - suggested_next_actions: 2–5 immediate, actionable remediation steps.
        - short_summary: one sentence, ≤ 120 characters, describing what is failing and why.
        - NEVER include in any output field: secrets, passwords, API keys, tokens, or
          connection strings, even if present in the incident labels or description.
        - Return ONLY the JSON object — no additional text.
        """;

    private static string BuildTriageUserMessage(IncidentTriageAiRequest r)
    {
        var labels = r.Labels.Count > 0
            ? Truncate(string.Join(", ", r.Labels.Select(kv => $"{kv.Key}={kv.Value}")), 300)
            : "(none)";

        return $"""
                Incident alert:
                - Alert: {r.AlertName}
                - Severity: {r.Severity}
                - Component: {r.Component}
                - Status: {r.Status}
                - Summary: {Truncate(r.Summary, 300)}
                - Description: {Truncate(r.Description, 500)}
                - Labels: {labels}
                - Firing since: {r.FiringSince:O}
                """;
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

    public async Task<AdminBillingInsightAiResult> GenerateAdminBillingInsightAsync(
        AdminBillingInsightAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("Google AI API key is not configured. Visit Admin → Settings to add one.");

        var body = new GenerateContentRequest(
            Contents:
            [
                new Content("user", [new Part(BuildAdminInsightUserMessage(request))]),
            ],
            SystemInstruction: new SystemInstruction([new Part(AdminInsightSystemPrompt)]),
            GenerationConfig: new GenerationConfig(600, null));

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(key, BuildGenerateContentPath(), body, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Google AI admin insight call timed out after {Timeout}s.", _opts.TimeoutSeconds);
            throw new AiUnavailableException("Google AI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Google AI admin insight network error.");
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
        var narrative = payload.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim()
                        ?? throw new AiUnavailableException("Google AI response did not include content.");

        if (string.IsNullOrWhiteSpace(narrative))
            throw new AiUnavailableException("Google AI returned an empty narrative.");

        return new AdminBillingInsightAiResult(
            narrative,
            new AiUsage(
                payload.UsageMetadata?.PromptTokenCount ?? 0,
                payload.UsageMetadata?.CandidatesTokenCount ?? 0,
                _opts.Model));
    }

    private const string AdminInsightSystemPrompt = """
        You are a concise business analyst for a multi-tenant restaurant SaaS platform.
        Analyse the provided billing and growth snapshot and return a plain-text narrative
        of 3–5 short paragraphs (150–300 words total). Cover: overall health, MRR trend,
        churn/risk signals, and one actionable recommendation. Use neutral business language.
        Do not use markdown, bullet points, or headers — plain prose only.
        """;

    private static string BuildAdminInsightUserMessage(AdminBillingInsightAiRequest r) =>
        $"""
         Platform snapshot for {r.Month}:

         Tenants: {r.TotalTenants} total ({r.ActiveTenants} active, {r.SuspendedTenants} suspended)
         Plans: {r.ProTenants} Pro, {r.FreeTenants} Free
         Billing: {r.PastDueTenants} past-due, {r.CanceledThisMonth} canceled this month, {r.NewProThisMonth} new Pro this month
         Estimated MRR: €{r.EstimatedMrr:F0}

         Top restaurants this month:
         {r.TopRestaurantsSummary}

         Weekly new-tenant growth (last 8 weeks):
         {r.WeeklyGrowthSummary}

         Write a concise platform health narrative.
         """;

    private async Task<HttpResponseMessage> SendAsync<T>(string apiKey, string path, T body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        request.Headers.Add("x-goog-api-key", apiKey);
        return await http.SendAsync(request, ct);
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
        [property: JsonPropertyName("response_mime_type")] string? ResponseMimeType);

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
