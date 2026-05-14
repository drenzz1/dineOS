import apiClient from "@/lib/api/apiClient";
import type { Shift, ShiftSummary, Priority } from "@/types";
import type { ShiftNoteFormValues } from "@/lib/validations/shiftNote";
import type { ShiftFormValues } from "@/lib/validations/shift";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

// ─── Internal DTOs ────────────────────────────────────────────────────────────

interface ShiftNoteDto {
  id: number;
  tenantId: number;
  title: string;
  body: string;
  priority: string;
  author: string;
  createdAt: string;
}

interface ShiftDto {
  id: number;
  tenantId: number;
  staffMemberId: number;
  staffName: string;
  startTime: string;
  endTime: string;
  notes?: string | null;
}

// ─── Mappers ──────────────────────────────────────────────────────────────────

function mapShiftNote(dto: ShiftNoteDto): ShiftSummary {
  return {
    id: String(dto.id),
    tenantId: String(dto.tenantId),
    title: dto.title,
    body: dto.body,
    priority: dto.priority.toLowerCase() as Priority,
    author: dto.author,
    createdAt: dto.createdAt,
  };
}

function mapShift(dto: ShiftDto): Shift {
  return {
    id: String(dto.id),
    tenantId: String(dto.tenantId),
    staffMemberId: String(dto.staffMemberId),
    staffName: dto.staffName,
    startTime: dto.startTime,
    endTime: dto.endTime,
    notes: dto.notes ?? undefined,
  };
}

// ─── Shift Notes ──────────────────────────────────────────────────────────────

export async function getShiftNotes(): Promise<ShiftSummary[]> {
  try {
    const res = await apiClient.get<ApiResponse<ShiftNoteDto[]>>("/v1/shifts/notes");
    return unwrap(res).map(mapShiftNote);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function saveShiftNote(
  data: ShiftNoteFormValues,
  author: string,
): Promise<ShiftSummary> {
  const priority = data.priority
    ? data.priority.charAt(0).toUpperCase() + data.priority.slice(1)
    : "Info";
  try {
    const res = await apiClient.post<ApiResponse<ShiftNoteDto>>("/v1/shifts/notes", {
      title: data.title,
      body: data.body,
      priority,
      author,
    });
    return mapShiftNote(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function deleteShiftNote(id: string): Promise<void> {
  try {
    await apiClient.delete(`/v1/shifts/notes/${id}`);
  } catch (error) {
    throw toApiError(error);
  }
}

// ─── Shifts ───────────────────────────────────────────────────────────────────

export async function getShifts(date?: string): Promise<Shift[]> {
  try {
    const res = await apiClient.get<ApiResponse<ShiftDto[]>>("/v1/shifts", {
      params: date ? { date } : undefined,
    });
    return unwrap(res).map(mapShift);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createShift(data: ShiftFormValues): Promise<Shift> {
  try {
    const res = await apiClient.post<ApiResponse<ShiftDto>>("/v1/shifts", {
      staffMemberId: data.staffMemberId,
      startTime: data.startTime,
      endTime: data.endTime,
      notes: data.notes || null,
    });
    return mapShift(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateShift(id: string, data: ShiftFormValues): Promise<Shift> {
  try {
    const res = await apiClient.put<ApiResponse<ShiftDto>>(`/v1/shifts/${id}`, {
      staffMemberId: data.staffMemberId,
      startTime: data.startTime,
      endTime: data.endTime,
      notes: data.notes || null,
    });
    return mapShift(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function deleteShift(id: string): Promise<void> {
  try {
    await apiClient.delete(`/v1/shifts/${id}`);
  } catch (error) {
    throw toApiError(error);
  }
}
