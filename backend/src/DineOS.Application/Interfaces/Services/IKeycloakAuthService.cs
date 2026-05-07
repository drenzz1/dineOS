using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IKeycloakAuthService
{
    Task<Result<RefreshTokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<RefreshTokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
