using DineOS.Application.Common;
using DineOS.Application.DemoAccess;

namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Public-facing demo access service (#216). Handles the three idempotency
/// branches (new email / active reuse / expired re-request) and the
/// honeypot bot-filter. Always returns a constant <see cref="RequestDemoAccessResponse"/>
/// so the API does not leak account existence.
/// </summary>
public interface IDemoAccessService
{
    Task<ServiceResult<RequestDemoAccessResponse>> RequestAsync(
        RequestDemoAccessRequest request,
        string? ipAddress,
        CancellationToken ct = default);
}
