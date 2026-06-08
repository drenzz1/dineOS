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

    /// <summary>
    /// Exchanges a valid, non-revoked staff refresh token for a fresh access
    /// token (no PIN re-entry). Re-checks that the staff member still exists and
    /// is active. The refresh token is echoed back unchanged with its remaining
    /// lifetime.
    /// </summary>
    Task<Result<StaffSessionResponse>> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Ends a staff session by blacklisting its access + refresh token ids until
    /// their natural expiry. Best-effort and idempotent — always completes.
    /// </summary>
    Task EndAsync(
        string? accessJti,
        long? accessExpiresAtUnix,
        string? refreshToken,
        CancellationToken ct = default);
}
