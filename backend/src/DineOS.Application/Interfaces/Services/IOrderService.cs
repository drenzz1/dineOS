using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Orders;

namespace DineOS.Application.Interfaces.Services;

public interface IOrderService
{
    Task<ServiceResult<List<OrderDto>>> GetOrdersAsync(
        DateOnly? date,
        string? status,
        CancellationToken ct = default);

    Task<ServiceResult<OrderDto>> GetOrderByIdAsync(long id, CancellationToken ct = default);

    Task<ServiceResult<OrderDto>> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<OrderDto>> UpdateStatusAsync(
        long id,
        UpdateOrderStatusRequest request,
        CancellationToken ct = default);
}
