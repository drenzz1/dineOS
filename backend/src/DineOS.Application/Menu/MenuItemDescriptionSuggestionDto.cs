namespace DineOS.Application.Menu;

public sealed record MenuItemDescriptionSuggestionDto(
    long MenuItemId,
    string ItemName,
    string Category,
    string SuggestedDescription,
    IReadOnlyList<string> SuggestedAllergens,
    AiSuggestionMetadata Metadata);

public sealed record AiSuggestionMetadata(
    string Model,
    int InputTokens,
    int OutputTokens,
    int LatencyMs);
