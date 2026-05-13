using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Shifts;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Shift scheduling endpoints — Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shifts")]
[Produces("application/json")]
[Authorize(Policy = Policies.ManagerAndAbove)]
[EnableRateLimiting("authenticated")]
public class ShiftsController(IShiftService shiftService) : ControllerBase
{
    /// <summary>Lists shifts for the current tenant, optionally filtered to those overlapping the given date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ShiftDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetShifts(
        [FromQuery] DateOnly? date,
        CancellationToken ct) =>
        (await shiftService.GetShiftsAsync(date, ct)).ToActionResult();

    /// <summary>Creates a new shift.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ShiftDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateShift(
        [FromBody] CreateShiftRequest request,
        CancellationToken ct) =>
        (await shiftService.CreateShiftAsync(request, ct)).ToActionResult();

    /// <summary>Updates a shift.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateShift(
        long id,
        [FromBody] UpdateShiftRequest request,
        CancellationToken ct) =>
        (await shiftService.UpdateShiftAsync(id, request, ct)).ToActionResult();

    /// <summary>Soft-deletes a shift.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ShiftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteShift(long id, CancellationToken ct) =>
        (await shiftService.DeleteShiftAsync(id, ct)).ToActionResult();
}
