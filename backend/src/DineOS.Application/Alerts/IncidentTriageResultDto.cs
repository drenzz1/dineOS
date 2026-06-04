using DineOS.Application.Interfaces.Services;

namespace DineOS.Application.Alerts;

public sealed record IncidentTriageResultDto(
    string CorrelationId,
    string AlertName,
    string Severity,
    IReadOnlyList<string> LikelyCauses,
    IReadOnlyList<string> SuggestedNextActions,
    string ShortSummary,
    AiUsage Usage);
