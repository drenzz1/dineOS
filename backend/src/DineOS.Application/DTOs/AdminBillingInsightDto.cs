using DineOS.Application.Menu;

namespace DineOS.Application.DTOs;

public sealed record AdminBillingInsightDto(
    string Narrative,
    AiSuggestionMetadata Metadata);
