using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.ShiftNotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Shift note endpoints — read: all authenticated staff; write: Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shifts/notes")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class ShiftNotesController(IShiftNoteService shiftNoteService) : ControllerBase
{
    /// <summary>Lists all shift notes for the current tenant, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ShiftNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetShiftNotes(CancellationToken ct) =>
        (await shiftNoteService.GetShiftNotesAsync(ct)).ToActionResult();

    /// <summary>Creates a new shift note.</summary>
    [HttpPost]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<ShiftNoteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateShiftNote(
        [FromBody] CreateShiftNoteRequest request,
        CancellationToken ct) =>
        (await shiftNoteService.CreateShiftNoteAsync(request, ct)).ToActionResult();

    /// <summary>Soft-deletes a shift note by ID.</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<ShiftNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteShiftNote(long id, CancellationToken ct) =>
        (await shiftNoteService.DeleteShiftNoteAsync(id, ct)).ToActionResult();
}
