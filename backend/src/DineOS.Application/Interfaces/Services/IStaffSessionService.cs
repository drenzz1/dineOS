using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Verifies a staff member's PIN within the current business (tenant) context
/// and mints a short-lived, role-scoped staff-session token. The caller must
/// already be authenticated as the business (Keycloak token) so the tenant is
/// established before the PIN is checked.
/// </summary>
public interface IStaffSessionService
{
    Task<Result<StaffSessionResponse>> StartAsync(
        StartStaffSessionRequest request,
        CancellationToken ct = default);
}
