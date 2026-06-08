using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.StaffMembers;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Staff management endpoints — Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/staff")]
[Produces("application/json")]
// Account-level: only the business Owner manages staff + PINs (#staff-pin-auth
// Phase 2). A PIN-selected operational staff member cannot create other staff.
[Authorize(Policy = Policies.OwnerOnly)]
[EnableRateLimiting("authenticated")]
public class StaffController(IStaffService staffService) : ControllerBase
{
    /// <summary>Lists all staff members for the current tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<StaffMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetStaff(CancellationToken ct) =>
        (await staffService.GetStaffAsync(ct)).ToActionResult();

    /// <summary>Adds a new staff member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AddStaff([FromBody] CreateStaffMemberRequest request, CancellationToken ct) =>
        (await staffService.CreateStaffAsync(request, ct)).ToActionResult();

    /// <summary>Updates a staff member's details.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateStaff(long id, [FromBody] UpdateStaffMemberRequest request, CancellationToken ct) =>
        (await staffService.UpdateStaffAsync(id, request, ct)).ToActionResult();

    /// <summary>Sets a staff member's active status.</summary>
    [HttpPatch("{id:long}/active")]
    [ProducesResponseType(typeof(ApiResponse<StaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SetStaffActive(long id, [FromBody] SetStaffActiveRequest request, CancellationToken ct) =>
        (await staffService.SetStaffActiveAsync(id, request, ct)).ToActionResult();
}
