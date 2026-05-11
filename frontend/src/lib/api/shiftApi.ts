// TODO: wire to real backend shift notes API — see GitHub issue #107
import type { ShiftSummary } from "@/types";
import type { ShiftNoteFormValues } from "@/lib/validations/shiftNote";

let mockShiftNotes: ShiftSummary[] = [];

export async function saveShiftNote(data: ShiftNoteFormValues): Promise<ShiftSummary> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  const created: ShiftSummary = {
    id: crypto.randomUUID(),
    title: data.title,
    body: data.body,
    priority: data.priority,
    author: "Manager",
    createdAt: new Date().toISOString(),
  };
  mockShiftNotes = [...mockShiftNotes, created];
  return created;
}

export async function getShiftNotes(): Promise<ShiftSummary[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockShiftNotes;
}

export async function deleteShiftNote(id: string): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  mockShiftNotes = mockShiftNotes.filter((n) => n.id !== id);
}
