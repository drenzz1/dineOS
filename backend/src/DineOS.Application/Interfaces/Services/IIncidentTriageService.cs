using DineOS.Application.Alerts;

namespace DineOS.Application.Interfaces.Services;

public interface IIncidentTriageService
{
    /// <summary>
    /// Normalises each alert in the payload, calls the AI triage client, and
    /// returns the results for every alert that was triaged successfully.
    /// Alerts whose triage fails (provider unavailable, unexpected error) are
    /// silently skipped and logged — the caller always receives a partial-or-full
    /// list, never an exception.
    /// </summary>
    Task<IReadOnlyList<IncidentTriageResultDto>> ProcessWebhookAsync(
        AlertmanagerWebhookPayload payload,
        CancellationToken ct = default);
}
