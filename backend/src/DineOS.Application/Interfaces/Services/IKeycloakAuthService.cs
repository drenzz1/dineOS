using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IKeycloakAuthService
{
    Task<Result<RefreshTokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<RefreshTokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a tenant owner's temporary password on first login (#205).
    /// Verifies the temporary password, clears Keycloak's
    /// <c>UPDATE_PASSWORD</c> required action, persists the new permanent
    /// password, and returns a usable token pair.
    /// </summary>
    Task<Result<RefreshTokenResponse>> ChangeFirstLoginPasswordAsync(
        FirstLoginPasswordChangeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the password for an already-authenticated user. Verifies the
    /// current password via Keycloak direct-grant, then resets to the new
    /// permanent password via the Admin API.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        string email,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
