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
}
