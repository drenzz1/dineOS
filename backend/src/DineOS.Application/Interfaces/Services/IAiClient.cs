namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Thin abstraction over an LLM provider. Controllers and application
/// services depend on this — not on Anthropic-specific types — so the
/// provider can be swapped or mocked without churning callers.
/// </summary>
public interface IAiClient
{
    Task<MenuDescriptionAiResult> GenerateMenuDescriptionAsync(
        MenuDescriptionAiRequest request,
        CancellationToken ct = default);

    Task<IncidentTriageAiResult> TriageIncidentAsync(
        IncidentTriageAiRequest request,
        CancellationToken ct = default);
}

public sealed record MenuDescriptionAiRequest(
    string Name,
    string Category,
    decimal Price,
    string? ExistingDescription);

public sealed record MenuDescriptionAiResult(
    string Description,
    IReadOnlyList<string> Allergens,
    AiUsage Usage);

public sealed record AiUsage(int InputTokens, int OutputTokens, string Model);

public sealed record IncidentTriageAiRequest(
    string AlertName,
    string Severity,
    string Component,
    string Status,
    string Summary,
    string Description,
    IReadOnlyList<KeyValuePair<string, string>> Labels,
    DateTimeOffset FiringSince);

public sealed record IncidentTriageAiResult(
    string Severity,
    IReadOnlyList<string> LikelyCauses,
    IReadOnlyList<string> SuggestedNextActions,
    string ShortSummary,
    AiUsage Usage);

/// <summary>Raised when the AI provider is unavailable, times out, or returns an unusable response.</summary>
public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
