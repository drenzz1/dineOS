using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.StaffMembers;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class StaffService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IPinHasher pinHasher,
    IValidator<CreateStaffMemberRequest> createValidator,
    IValidator<UpdateStaffMemberRequest> updateValidator,
    ILogger<StaffService> logger) : IStaffService
{
    public async Task<ServiceResult<List<StaffMemberDto>>> GetStaffAsync(CancellationToken ct = default)
    {
        var staff = await db.StaffMembers
            .Select(s => new StaffMemberDto
            {
                Id = s.Id,
                FullName = s.FullName,
                Email = s.Email,
                Role = s.Role,
                IsActive = s.IsActive,
                TenantId = s.TenantId
            })
            .ToListAsync(ct);

        return ServiceResult<List<StaffMemberDto>>.Ok(staff, "Staff list");
    }

    public async Task<ServiceResult<StaffMemberDto>> CreateStaffAsync(
        CreateStaffMemberRequest request,
        CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<StaffMemberDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<StaffMemberDto>.BadRequest("Tenant context is required.");

        var staff = new StaffMember
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = request.Role,
            PinHash = pinHasher.Hash(request.Pin),
            IsActive = true,
            TenantId = tenantId
        };

        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Staff created: StaffId={StaffId} TenantId={TenantId} ActorUserId={ActorUserId} Role={Role}",
            staff.Id, tenantId, currentUserService.UserId, staff.Role);

        return ServiceResult<StaffMemberDto>.Created(ToDto(staff), "Staff member added");
    }

    public async Task<ServiceResult<StaffMemberDto>> UpdateStaffAsync(
        long id,
        UpdateStaffMemberRequest request,
        CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<StaffMemberDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (staff is null)
            return ServiceResult<StaffMemberDto>.NotFound($"Staff member {id} not found.");

        if (request.FullName is not null) staff.FullName = request.FullName;
        if (request.Email is not null) staff.Email = request.Email;
        if (request.Role is not null) staff.Role = request.Role;
        if (request.Pin is not null) staff.PinHash = pinHasher.Hash(request.Pin);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Staff updated: StaffId={StaffId} TenantId={TenantId} ActorUserId={ActorUserId}",
            staff.Id, staff.TenantId, currentUserService.UserId);

        return ServiceResult<StaffMemberDto>.Ok(ToDto(staff), "Staff member updated");
    }

    public async Task<ServiceResult<StaffMemberDto>> SetStaffActiveAsync(
        long id,
        SetStaffActiveRequest request,
        CancellationToken ct = default)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (staff is null)
            return ServiceResult<StaffMemberDto>.NotFound($"Staff member {id} not found.");

        var previous = staff.IsActive;
        staff.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Staff active-status changed: StaffId={StaffId} TenantId={TenantId} ActorUserId={ActorUserId} Previous={Previous} Current={Current}",
            staff.Id, staff.TenantId, currentUserService.UserId, previous, staff.IsActive);

        return ServiceResult<StaffMemberDto>.Ok(
            ToDto(staff),
            $"Staff member {id} is now {(staff.IsActive ? "active" : "inactive")}.");
    }

    private static StaffMemberDto ToDto(StaffMember s) => new()
    {
        Id = s.Id,
        FullName = s.FullName,
        Email = s.Email,
        Role = s.Role,
        IsActive = s.IsActive,
        TenantId = s.TenantId
    };
}
