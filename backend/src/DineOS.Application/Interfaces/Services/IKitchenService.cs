using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Kitchen;

namespace DineOS.Application.Interfaces.Services;

public interface IKitchenService
{
    Task<ServiceResult<List<OrderDto>>> GetKitchenOrdersAsync(CancellationToken ct = default);

    Task<ServiceResult<OrderDto>> UpdateOrderStatusAsync(
        long id,
        UpdateKitchenOrderStatusRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<KitchenQueueSummaryDto>> GetQueueSummaryAsync(CancellationToken ct = default);
}
