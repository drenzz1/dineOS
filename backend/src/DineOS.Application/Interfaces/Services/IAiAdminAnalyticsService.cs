using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IAiAdminAnalyticsService
{
    Task<ServiceResult<AdminBillingInsightDto>> GenerateInsightAsync(CancellationToken ct = default);
}
