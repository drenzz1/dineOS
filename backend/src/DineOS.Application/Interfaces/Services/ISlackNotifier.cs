using DineOS.Application.Alerts;

namespace DineOS.Application.Interfaces.Services;

public interface ISlackNotifier
{
    Task NotifyTriageAsync(IncidentTriageResultDto result, CancellationToken ct = default);
}
