"use client";

import type { ShiftSummary, Priority } from "@/types";
import { Button } from "@/components/ui/Button";

interface ShiftNoteCardProps {
  note: ShiftSummary;
  onDelete: (id: string) => void;
}

const BADGE: Record<Priority, string> = {
  info: "bg-blue-100 text-blue-700",
  warning: "bg-amber-100 text-amber-700",
  urgent: "bg-red-100 text-red-700",
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export default function ShiftNoteCard({ note, onDelete }: ShiftNoteCardProps) {
  return (
    <li className="rounded-lg border border-zinc-200 bg-white p-4 space-y-2">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          {note.priority && (
            <span
              className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium capitalize ${BADGE[note.priority]}`}
            >
              {note.priority}
            </span>
          )}
          <p className="truncate font-medium text-zinc-900">{note.title}</p>
        </div>
        <Button
          variant="danger"
          size="sm"
          onClick={() => onDelete(note.id)}
        >
          Delete
        </Button>
      </div>

      <p className="line-clamp-2 text-sm text-zinc-600">{note.body}</p>

      <p className="text-xs text-zinc-400">
        {note.author} · {formatDate(note.createdAt)}
      </p>
    </li>
  );
}
