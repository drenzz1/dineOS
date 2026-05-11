using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Payments;

namespace DineOS.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<ServiceResult<List<OrderDto>>> GetOpenOrdersAsync(CancellationToken ct = default);

    Task<ServiceResult<PaymentDto>> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken ct = default);
}
