using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.ShiftNotes;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>Shift note endpoints — read: all authenticated staff; write: Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shifts/notes")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class ShiftNotesController(
    AppDbContext db,
    ITenantService tenantService,
    IValidator<CreateShiftNoteRequest> createValidator) : ControllerBase
{
    /// <summary>Lists all shift notes for the current tenant, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ShiftNoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetShiftNotes(CancellationToken ct)
    {
        var notes = await db.ShiftNotes
            .AsNoTracking()
            .OrderByDescending(sn => sn.CreatedAt)
            .Select(sn => new ShiftNoteDto
            {
                Id       = sn.Id,
                Title    = sn.Title,
                Body     = sn.Body,
                Priority = sn.Priority.ToString(),
                Author   = sn.Author,
                TenantId = sn.TenantId,
                CreatedAt = sn.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<ShiftNoteDto>>.Ok(notes, "Shift notes"));
    }

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
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

        var note = new ShiftNote
        {
            TenantId = tenantId,
            Title    = request.Title,
            Body     = request.Body,
            Priority = Enum.Parse<ShiftNotePriority>(request.Priority),
            Author   = request.Author,
        };

        db.ShiftNotes.Add(note);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ShiftNoteDto>.Ok(ToDto(note), "Shift note created."));
    }

    /// <summary>Soft-deletes a shift note by ID.</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<ShiftNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteShiftNote(long id, CancellationToken ct)
    {
        var note = await db.ShiftNotes.FirstOrDefaultAsync(sn => sn.Id == id, ct);

        if (note is null)
            return NotFound(ApiResponse.Fail($"Shift note {id} not found."));

        db.ShiftNotes.Remove(note);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<ShiftNoteDto>.Ok(ToDto(note), $"Shift note {id} deleted."));
    }

    private static ShiftNoteDto ToDto(ShiftNote sn) => new()
    {
        Id        = sn.Id,
        Title     = sn.Title,
        Body      = sn.Body,
        Priority  = sn.Priority.ToString(),
        Author    = sn.Author,
        TenantId  = sn.TenantId,
        CreatedAt = sn.CreatedAt,
    };
}
