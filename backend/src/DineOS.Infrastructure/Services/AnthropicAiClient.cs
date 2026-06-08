using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// Anthropic Messages API client. Uses the JSON-tool-use pattern so the model
/// returns a structured payload that doesn't require fragile text parsing.
/// </summary>
public sealed class AnthropicAiClient(
    HttpClient http,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicAiClient> logger) : IAiClient
{
    public const string HttpClientName = "anthropic";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AnthropicOptions _opts = options.Value;

    public async Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new AiUnavailableException("Anthropic API key is not configured (Anthropic:ApiKey).");

        var userMessage = BuildUserMessage(request);

        var body = new MessagesRequest(
            Model:      _opts.Model,
            MaxTokens:  _opts.MaxTokens,
            System:     SystemPrompt,
            Tools:      [DescriptionTool],
            ToolChoice: new ToolChoice("tool", DescriptionTool.Name),
            Messages:   [new Message("user", userMessage)]);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/v1/messages", body, JsonOpts, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Anthropic call timed out after {Timeout}s. Item={Name}",
                _opts.TimeoutSeconds, request.Name);
            throw new AiUnavailableException("Anthropic request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "Anthropic network error. Item={Name}",
                request.Name);
            throw new AiUnavailableException("Anthropic network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Anthropic returned {StatusCode}. Snippet={Snippet}",
                (int)response.StatusCode, Truncate(snippet, 400));
            throw new AiUnavailableException(
                $"Anthropic returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("Anthropic response was empty.");

        var toolUse = payload.Content?.FirstOrDefault(c => c.Type == "tool_use")
            ?? throw new AiUnavailableException("Anthropic response did not include a tool_use block.");

        // tool_use.input is the structured arguments dictionary.
        var description = toolUse.Input?.TryGetProperty("description", out var d) == true
            ? d.GetString() ?? string.Empty
            : string.Empty;

        var allergens = new List<string>();
        if (toolUse.Input?.TryGetProperty("allergens", out var arr) == true && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    allergens.Add(s);
            }
        }

        if (string.IsNullOrWhiteSpace(description))
            throw new AiUnavailableException("Anthropic returned an empty description.");

        var usage = new AiUsage(
            payload.Usage?.InputTokens  ?? 0,
            payload.Usage?.OutputTokens ?? 0,
            _opts.Model);

        return new MenuDescriptionAiResult(description.Trim(), allergens, usage);
    }

    public async Task<IncidentTriageAiResult> TriageIncidentAsync(
        IncidentTriageAiRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
            throw new AiUnavailableException("Anthropic API key is not configured (Anthropic:ApiKey).");

        var body = new MessagesRequest(
            Model:      _opts.Model,
            MaxTokens:  _opts.MaxTokens,
            System:     TriageSystemPrompt,
            Tools:      [TriageTool],
            ToolChoice: new ToolChoice("tool", TriageTool.Name),
            Messages:   [new Message("user", BuildTriageUserMessage(request))]);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/v1/messages", body, JsonOpts, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Anthropic triage call timed out after {Timeout}s. Alert={AlertName}",
                _opts.TimeoutSeconds, request.AlertName);
            throw new AiUnavailableException("Anthropic request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "Anthropic triage network error. Alert={AlertName}",
                request.AlertName);
            throw new AiUnavailableException("Anthropic network error.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var snippet = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Anthropic returned {StatusCode}. Snippet={Snippet}",
                (int)response.StatusCode, Truncate(snippet, 400));
            throw new AiUnavailableException(
                $"Anthropic returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<MessagesResponse>(JsonOpts, ct)
                      ?? throw new AiUnavailableException("Anthropic response was empty.");

        var toolUse = payload.Content?.FirstOrDefault(c => c.Type == "tool_use")
            ?? throw new AiUnavailableException("Anthropic response did not include a tool_use block.");

        var severity = toolUse.Input?.TryGetProperty("severity", out var sev) == true
            ? sev.GetString() ?? string.Empty
            : string.Empty;

        var likelyCauses        = ParseStringArray(toolUse.Input, "likely_causes");
        var suggestedNextActions = ParseStringArray(toolUse.Input, "suggested_next_actions");

        var shortSummary = toolUse.Input?.TryGetProperty("short_summary", out var ss) == true
            ? ss.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(shortSummary))
            throw new AiUnavailableException("Anthropic returned an empty short_summary.");

        var usage = new AiUsage(
            payload.Usage?.InputTokens  ?? 0,
            payload.Usage?.OutputTokens ?? 0,
            _opts.Model);

        return new IncidentTriageAiResult(
            severity.Trim(),
            likelyCauses,
            suggestedNextActions,
            shortSummary.Trim(),
            usage);
    }

    // ── Prompt + tool definition ──────────────────────────────────────────
    private const string SystemPrompt = """
        You are a helpful assistant for a restaurant POS. Generate concise,
        appetizing menu copy.

        Rules:
        - description: 1–2 sentences, max 200 characters, no marketing fluff,
          present tense, customer-facing.
        - allergens: only entries that are clearly implied by the dish (e.g.
          "gluten", "dairy", "shellfish", "nuts", "soy", "egg"). Empty array
          if nothing is clearly implied. Do not invent allergens.
        - Always return your answer through the `report_menu_description` tool.
        """;

    private const string TriageSystemPrompt = """
        You are an SRE incident-triage assistant. Analyse the incoming alert and
        return ONLY structured triage data through the `report_incident_triage` tool.

        Rules:
        - severity: re-assess as critical|high|medium|low based on context.
        - likely_causes: 1–5 concise probable root causes, no duplication.
        - suggested_next_actions: 2–5 immediate, actionable remediation steps.
        - short_summary: one sentence, ≤ 120 characters, describing what is failing and why.
        - NEVER include in any output field: secrets, passwords, API keys, tokens, or
          connection strings, even if present in the incident labels or description.
        - Return ONLY through the tool — no additional text.
        """;

    private static readonly Tool TriageTool = new(
        Name: "report_incident_triage",
        Description: "Reports the triage assessment for an infrastructure or application incident.",
        InputSchema: new ToolSchema(
            Type: "object",
            Properties: new Dictionary<string, ToolProp>
            {
                ["severity"]               = new("string", "Assessed severity: critical|high|medium|low."),
                ["likely_causes"]          = new("array",  "1–5 probable root causes of the incident.")
                                             { Items = new ToolProp("string", null) },
                ["suggested_next_actions"] = new("array",  "2–5 immediate actionable remediation steps.")
                                             { Items = new ToolProp("string", null) },
                ["short_summary"]          = new("string", "One sentence (≤ 120 chars) describing what is failing and why."),
            },
            Required: ["severity", "likely_causes", "suggested_next_actions", "short_summary"]));

    private static readonly Tool DescriptionTool = new(
        Name: "report_menu_description",
        Description: "Reports the suggested customer-facing description and allergen list for a menu item.",
        InputSchema: new ToolSchema(
            Type: "object",
            Properties: new Dictionary<string, ToolProp>
            {
                ["description"] = new("string", "1–2 sentence customer-facing description, max 200 characters."),
                ["allergens"]   = new("array",  "Likely allergens implied by the dish. Use lowercase single-word labels.")
                                    { Items = new ToolProp("string", null) },
            },
            Required: ["description", "allergens"]));

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

                Generate a new description and allergen list via the tool.
                """;
    }

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

                Triage this incident via the tool.
                """;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement? element, string propertyName)
    {
        var result = new List<string>();
        if (element?.TryGetProperty(propertyName, out var arr) == true && arr.ValueKind == JsonValueKind.Array)
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

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    // ── Wire types — kept private so callers don't depend on the schema ───
    private sealed record MessagesRequest(
        string  Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        string  System,
        Tool[]  Tools,
        [property: JsonPropertyName("tool_choice")] ToolChoice ToolChoice,
        Message[] Messages);

    private sealed record Message(string Role, string Content);

    private sealed record Tool(
        string Name,
        string Description,
        [property: JsonPropertyName("input_schema")] ToolSchema InputSchema);

    private sealed record ToolSchema(
        string Type,
        Dictionary<string, ToolProp> Properties,
        string[] Required);

    private sealed record ToolProp(string Type, string? Description)
    {
        [JsonPropertyName("items")]
        public ToolProp? Items { get; init; }
    }

    private sealed record ToolChoice(string Type, string Name);

    private sealed record MessagesResponse(
        ContentBlock[]? Content,
        Usage? Usage);

    private sealed record ContentBlock(
        string Type,
        string? Text,
        string? Name,
        JsonElement? Input);

    private sealed record Usage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
