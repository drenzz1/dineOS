using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.StaffMembers;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>Staff management endpoints — Manager and above.</summary>
[ApiController]
[Route("api/v1/staff")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
public class StaffController(
    AppDbContext db,
    ITenantService tenantService,
    IPinHasher pinHasher,
    IValidator<CreateStaffMemberRequest> createValidator,
    IValidator<UpdateStaffMemberRequest> updateValidator) : ControllerBase
{
    /// <summary>Lists all staff members for the current tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<StaffMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaff()
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
            .ToListAsync();

        return Ok(ApiResponse<List<StaffMemberDto>>.Ok(staff, "Staff list"));
    }

    /// <summary>Adds a new staff member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddStaff([FromBody] CreateStaffMemberRequest request)
    {
        var validation = await createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

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
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<StaffMemberDto>.Ok(ToDto(staff), "Staff member added"));
    }

    /// <summary>Updates a staff member's details.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStaff(long id, [FromBody] UpdateStaffMemberRequest request)
    {
        var validation = await updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null)
            return NotFound(ApiResponse.Fail($"Staff member {id} not found."));

        if (request.FullName is not null) staff.FullName = request.FullName;
        if (request.Email is not null) staff.Email = request.Email;
        if (request.Role is not null) staff.Role = request.Role;
        if (request.Pin is not null) staff.PinHash = pinHasher.Hash(request.Pin);

        await db.SaveChangesAsync();

        return Ok(ApiResponse<StaffMemberDto>.Ok(ToDto(staff), "Staff member updated"));
    }

    /// <summary>Sets a staff member's active status.</summary>
    [HttpPatch("{id:long}/active")]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStaffActive(long id, [FromBody] SetStaffActiveRequest request)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null)
            return NotFound(ApiResponse.Fail($"Staff member {id} not found."));

        staff.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Ok(ApiResponse<StaffMemberDto>.Ok(ToDto(staff),
            $"Staff member {id} is now {(request.IsActive ? "active" : "inactive")}."));
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
