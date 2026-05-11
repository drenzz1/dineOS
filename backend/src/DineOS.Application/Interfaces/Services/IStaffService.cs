using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.StaffMembers;

namespace DineOS.Application.Interfaces.Services;

public interface IStaffService
{
    Task<ServiceResult<List<StaffMemberDto>>> GetStaffAsync(CancellationToken ct = default);

    Task<ServiceResult<StaffMemberDto>> CreateStaffAsync(
        CreateStaffMemberRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<StaffMemberDto>> UpdateStaffAsync(
        long id,
        UpdateStaffMemberRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<StaffMemberDto>> SetStaffActiveAsync(
        long id,
        SetStaffActiveRequest request,
        CancellationToken ct = default);
}
