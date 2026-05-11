using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.ShiftNotes;

namespace DineOS.Application.Interfaces.Services;

public interface IShiftNoteService
{
    Task<ServiceResult<List<ShiftNoteDto>>> GetShiftNotesAsync(CancellationToken ct = default);

    Task<ServiceResult<ShiftNoteDto>> CreateShiftNoteAsync(
        CreateShiftNoteRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ShiftNoteDto>> DeleteShiftNoteAsync(long id, CancellationToken ct = default);
}
