"use client";

import type { ShiftSummary } from "@/types";
import ShiftNoteCard from "./ShiftNoteCard";

interface ShiftNoteListProps {
  notes: ShiftSummary[];
  onDelete: (id: string) => void;
}

export default function ShiftNoteList({ notes, onDelete }: ShiftNoteListProps) {
  if (notes.length === 0) {
    return (
      <div className="flex items-center justify-center py-16 text-sm text-zinc-600">
        No shift notes yet.
      </div>
    );
  }

  return (
    <ul className="space-y-3">
      {notes.map((note) => (
        <ShiftNoteCard key={note.id} note={note} onDelete={onDelete} />
      ))}
    </ul>
  );
}
