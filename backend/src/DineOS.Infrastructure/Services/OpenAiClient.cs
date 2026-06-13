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
    ILogger<OpenAiClient> logger,
    string? apiKeyOverride = null) : IAiClient
{
    public const string HttpClientName = "openai";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OpenAiOptions _opts = options.Value;

    private string EffectiveApiKey =>
        !string.IsNullOrWhiteSpace(apiKeyOverride) ? apiKeyOverride : _opts.ApiKey;

    public async Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("OpenAI API key is not configured. Visit Admin → Settings to add one.");

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
            response = await SendAsync(key, "/v1/chat/completions", body, ct);
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

    public async Task<IncidentTriageAiResult> TriageIncidentAsync(
        IncidentTriageAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("OpenAI API key is not configured. Visit Admin → Settings to add one.");

        var body = new ChatCompletionRequest(
            Model: _opts.Model,
            MaxTokens: _opts.MaxTokens,
            ResponseFormat: new ResponseFormat("json_object"),
            Messages:
            [
                new Message("system", TriageSystemPrompt),
                new Message("user", BuildTriageUserMessage(request)),
            ]);

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(key, "/v1/chat/completions", body, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenAI triage call timed out after {Timeout}s. Alert={AlertName}", _opts.TimeoutSeconds, request.AlertName);
            throw new AiUnavailableException("OpenAI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OpenAI triage network error. Alert={AlertName}", request.AlertName);
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

        var parsed = ParseTriage(content, "OpenAI");
        return new IncidentTriageAiResult(
            parsed.Severity,
            parsed.LikelyCauses,
            parsed.SuggestedNextActions,
            parsed.ShortSummary,
            new AiUsage(
                payload.Usage?.PromptTokens ?? 0,
                payload.Usage?.CompletionTokens ?? 0,
                payload.Model ?? _opts.Model));
    }

    internal static (string Severity, IReadOnlyList<string> LikelyCauses, IReadOnlyList<string> SuggestedNextActions, string ShortSummary) ParseTriage(string content, string providerName)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var severity = root.TryGetProperty("severity", out var sev)
                ? sev.GetString() ?? string.Empty
                : string.Empty;

            var likelyCauses        = ParseStringArray(root, "likely_causes");
            var suggestedNextActions = ParseStringArray(root, "suggested_next_actions");

            var shortSummary = root.TryGetProperty("short_summary", out var ss)
                ? ss.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(shortSummary))
                throw new AiUnavailableException($"{providerName} returned an empty short_summary.");

            return (severity.Trim(), likelyCauses, suggestedNextActions, shortSummary.Trim());
        }
        catch (JsonException ex)
        {
            throw new AiUnavailableException($"{providerName} returned invalid JSON.", ex);
        }
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement element, string propertyName)
    {
        var result = new List<string>();
        if (element.TryGetProperty(propertyName, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(s);
            }
        }
        return result;
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

    public async Task<AdminBillingInsightAiResult> GenerateAdminBillingInsightAsync(
        AdminBillingInsightAiRequest request,
        CancellationToken ct = default)
    {
        var key = EffectiveApiKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new AiUnavailableException("OpenAI API key is not configured. Visit Admin → Settings to add one.");

        var body = new PlainChatCompletionRequest(
            Model: _opts.Model,
            MaxTokens: 600,
            Messages:
            [
                new Message("system", AdminInsightSystemPrompt),
                new Message("user", BuildAdminInsightUserMessage(request)),
            ]);

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(key, "/v1/chat/completions", body, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenAI admin insight call timed out after {Timeout}s.", _opts.TimeoutSeconds);
            throw new AiUnavailableException("OpenAI request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OpenAI admin insight network error.");
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
        var narrative = payload.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                        ?? throw new AiUnavailableException("OpenAI response did not include content.");

        if (string.IsNullOrWhiteSpace(narrative))
            throw new AiUnavailableException("OpenAI returned an empty narrative.");

        return new AdminBillingInsightAiResult(
            narrative,
            new AiUsage(
                payload.Usage?.PromptTokens ?? 0,
                payload.Usage?.CompletionTokens ?? 0,
                payload.Model ?? _opts.Model));
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
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        return await http.SendAsync(request, ct);
    }

<<<<<<< HEAD

=======
>>>>>>> 5948630 (feat(admin): AI provider settings page — choose, key, test, save)
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed record PlainChatCompletionRequest(
        string Model,
        Message[] Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

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
