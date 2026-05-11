using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IAdminService
{
    Task<ServiceResult<PagedResponse<PlatformUserDto>>> ListUsersAsync(
        string? search,
        PagedRequest pagination,
        CancellationToken ct = default);
}
