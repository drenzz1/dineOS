using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Restaurants;

namespace DineOS.Application.Interfaces.Services;

public interface IAdminRestaurantService
{
    Task<ServiceResult<PagedResponse<RestaurantDto>>> ListAsync(
        string? search,
        PagedRequest pagination,
        CancellationToken ct = default);

    Task<ServiceResult<RestaurantDto>> GetByIdAsync(long id, CancellationToken ct = default);

    Task<ServiceResult<RestaurantDto>> CreateAsync(
        CreateRestaurantRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<RestaurantDto>> UpdateStatusAsync(
        long id,
        UpdateRestaurantStatusRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<RestaurantDto>> UpdatePlanAsync(
        long id,
        UpdateRestaurantPlanRequest request,
        CancellationToken ct = default);
}
