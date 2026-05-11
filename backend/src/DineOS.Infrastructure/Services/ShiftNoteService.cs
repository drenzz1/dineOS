using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.ShiftNotes;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class ShiftNoteService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<CreateShiftNoteRequest> createValidator,
    ILogger<ShiftNoteService> logger) : IShiftNoteService
{
    public async Task<ServiceResult<List<ShiftNoteDto>>> GetShiftNotesAsync(CancellationToken ct = default)
    {
        var notes = await db.ShiftNotes
            .AsNoTracking()
            .OrderByDescending(sn => sn.CreatedAt)
            .Select(sn => new ShiftNoteDto
            {
                Id        = sn.Id,
                Title     = sn.Title,
                Body      = sn.Body,
                Priority  = sn.Priority.ToString(),
                Author    = sn.Author,
                TenantId  = sn.TenantId,
                CreatedAt = sn.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<ShiftNoteDto>>.Ok(notes, "Shift notes");
    }

    public async Task<ServiceResult<ShiftNoteDto>> CreateShiftNoteAsync(
        CreateShiftNoteRequest request,
        CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<ShiftNoteDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<ShiftNoteDto>.BadRequest("Tenant context is required.");

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

        logger.LogInformation(
            "Shift note created: ShiftNoteId={ShiftNoteId} TenantId={TenantId} ActorUserId={ActorUserId} Priority={Priority}",
            note.Id, tenantId, currentUserService.UserId, note.Priority);

        return ServiceResult<ShiftNoteDto>.Created(ToDto(note), "Shift note created.");
    }

    public async Task<ServiceResult<ShiftNoteDto>> DeleteShiftNoteAsync(long id, CancellationToken ct = default)
    {
        var note = await db.ShiftNotes.FirstOrDefaultAsync(sn => sn.Id == id, ct);
        if (note is null)
            return ServiceResult<ShiftNoteDto>.NotFound($"Shift note {id} not found.");

        db.ShiftNotes.Remove(note);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Shift note deleted: ShiftNoteId={ShiftNoteId} TenantId={TenantId} ActorUserId={ActorUserId}",
            note.Id, note.TenantId, currentUserService.UserId);

        return ServiceResult<ShiftNoteDto>.Ok(ToDto(note), $"Shift note {id} deleted.");
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
