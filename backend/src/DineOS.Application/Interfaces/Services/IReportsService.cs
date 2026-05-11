using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IReportsService
{
    Task<ServiceResult<SalesReportDto>> GetSalesReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<ServiceResult<OrdersReportDto>> GetOrdersReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<ServiceResult<StaffReportDto>> GetStaffReportAsync(CancellationToken ct = default);
}
