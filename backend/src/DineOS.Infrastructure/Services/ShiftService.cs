using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Shifts;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class ShiftService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<CreateShiftRequest> createValidator,
    IValidator<UpdateShiftRequest> updateValidator,
    ILogger<ShiftService> logger) : IShiftService
{
    public async Task<ServiceResult<List<ShiftDto>>> GetShiftsAsync(DateOnly? date, CancellationToken ct = default)
    {
        var query = from s in db.Shifts.AsNoTracking()
                    join sm in db.StaffMembers.AsNoTracking() on s.StaffMemberId equals sm.Id into smj
                    from sm in smj.DefaultIfEmpty()
                    select new { Shift = s, StaffName = sm != null ? sm.FullName : "(unknown)" };

        if (date is { } d)
        {
            var start = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end   = d.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.Shift.StartTime <= end && x.Shift.EndTime >= start);
        }

        var rows = await query
            .OrderBy(x => x.Shift.StartTime)
            .ToListAsync(ct);

        var dtos = rows.Select(r => ToDto(r.Shift, r.StaffName)).ToList();
        return ServiceResult<List<ShiftDto>>.Ok(dtos, "Shifts");
    }

    public async Task<ServiceResult<ShiftDto>> CreateShiftAsync(
        CreateShiftRequest request,
        CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<ShiftDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<ShiftDto>.BadRequest("Tenant context is required.");

        var staff = await db.StaffMembers.FirstOrDefaultAsync(sm => sm.Id == request.StaffMemberId, ct);
        if (staff is null)
            return ServiceResult<ShiftDto>.BadRequest($"Staff member {request.StaffMemberId} not found for this tenant.");

        var shift = new Shift
        {
            TenantId      = tenantId,
            StaffMemberId = request.StaffMemberId,
            StartTime     = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc),
            EndTime       = DateTime.SpecifyKind(request.EndTime, DateTimeKind.Utc),
            Notes         = request.Notes,
        };

        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Shift created: ShiftId={ShiftId} TenantId={TenantId} StaffMemberId={StaffMemberId} ActorUserId={ActorUserId}",
            shift.Id, tenantId, shift.StaffMemberId, currentUserService.UserId);

        return ServiceResult<ShiftDto>.Created(ToDto(shift, staff.FullName), "Shift created.");
    }

    public async Task<ServiceResult<ShiftDto>> UpdateShiftAsync(
        long id,
        UpdateShiftRequest request,
        CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<ShiftDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shift is null)
            return ServiceResult<ShiftDto>.NotFound($"Shift {id} not found.");

        if (request.StaffMemberId is { } newStaffId)
        {
            var newStaffExists = await db.StaffMembers.AnyAsync(sm => sm.Id == newStaffId, ct);
            if (!newStaffExists)
                return ServiceResult<ShiftDto>.BadRequest($"Staff member {newStaffId} not found for this tenant.");
            shift.StaffMemberId = newStaffId;
        }

        var newStart = request.StartTime ?? shift.StartTime;
        var newEnd   = request.EndTime   ?? shift.EndTime;
        if (newEnd <= newStart)
            return ServiceResult<ShiftDto>.ValidationFailed(
                "Validation failed",
                ["EndTime must be after StartTime."]);

        if (request.StartTime is not null)
            shift.StartTime = DateTime.SpecifyKind(request.StartTime.Value, DateTimeKind.Utc);
        if (request.EndTime is not null)
            shift.EndTime = DateTime.SpecifyKind(request.EndTime.Value, DateTimeKind.Utc);
        if (request.Notes is not null)
            shift.Notes = request.Notes;

        await db.SaveChangesAsync(ct);

        var staffName = await db.StaffMembers
            .Where(sm => sm.Id == shift.StaffMemberId)
            .Select(sm => sm.FullName)
            .FirstOrDefaultAsync(ct) ?? "(unknown)";

        logger.LogInformation(
            "Shift updated: ShiftId={ShiftId} TenantId={TenantId} ActorUserId={ActorUserId}",
            shift.Id, shift.TenantId, currentUserService.UserId);

        return ServiceResult<ShiftDto>.Ok(ToDto(shift, staffName), "Shift updated.");
    }

    public async Task<ServiceResult<ShiftDto>> DeleteShiftAsync(long id, CancellationToken ct = default)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shift is null)
            return ServiceResult<ShiftDto>.NotFound($"Shift {id} not found.");

        var staffName = await db.StaffMembers
            .Where(sm => sm.Id == shift.StaffMemberId)
            .Select(sm => sm.FullName)
            .FirstOrDefaultAsync(ct) ?? "(unknown)";

        db.Shifts.Remove(shift);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Shift deleted: ShiftId={ShiftId} TenantId={TenantId} ActorUserId={ActorUserId}",
            shift.Id, shift.TenantId, currentUserService.UserId);

        return ServiceResult<ShiftDto>.Ok(ToDto(shift, staffName), $"Shift {id} deleted.");
    }

    private static ShiftDto ToDto(Shift s, string staffName) => new()
    {
        Id            = s.Id,
        TenantId      = s.TenantId,
        StaffMemberId = s.StaffMemberId,
        StaffName     = staffName,
        StartTime     = s.StartTime,
        EndTime       = s.EndTime,
        Notes         = s.Notes,
    };
}
