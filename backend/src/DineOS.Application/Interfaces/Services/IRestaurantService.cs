using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.RestaurantProfile;
using DineOS.Application.RestaurantTables;

namespace DineOS.Application.Interfaces.Services;

public interface IRestaurantService
{
    Task<ServiceResult<RestaurantProfileDto>> GetProfileAsync(CancellationToken ct = default);

    Task<ServiceResult<RestaurantProfileDto>> UpdateProfileAsync(
        UpdateRestaurantProfileRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<List<RestaurantTableDto>>> ListTablesAsync(CancellationToken ct = default);

    Task<ServiceResult<RestaurantTableDto>> AddTableAsync(
        CreateRestaurantTableRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<RestaurantTableDto>> UpdateTableAsync(
        long id,
        UpdateRestaurantTableRequest request,
        CancellationToken ct = default);
}
