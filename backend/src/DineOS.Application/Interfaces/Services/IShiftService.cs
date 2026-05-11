using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Shifts;

namespace DineOS.Application.Interfaces.Services;

public interface IShiftService
{
    Task<ServiceResult<List<ShiftDto>>> GetShiftsAsync(DateOnly? date, CancellationToken ct = default);

    Task<ServiceResult<ShiftDto>> CreateShiftAsync(
        CreateShiftRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ShiftDto>> UpdateShiftAsync(
        long id,
        UpdateShiftRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ShiftDto>> DeleteShiftAsync(long id, CancellationToken ct = default);
}
