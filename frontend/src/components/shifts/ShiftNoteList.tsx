"use client";

import type { ShiftSummary } from "@/types";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";
import ShiftNoteCard from "./ShiftNoteCard";

interface ShiftNoteListProps {
  notes: ShiftSummary[];
  onDelete: (id: string) => void;
}

export default function ShiftNoteList({ notes, onDelete }: ShiftNoteListProps) {
  if (notes.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-border-strong bg-surface">
        <EmptyState
          illustration={<Illo.Note />}
          title="No shift notes yet"
          description="Capture the kind of things you'd want the next shift to know — counts, repairs, VIPs, unusual incidents."
        />
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
