"use client";

import type { ShiftSummary, Priority } from "@/types";
import { Button } from "@/components/ui/Button";

interface ShiftNoteCardProps {
  note: ShiftSummary;
  canDelete: boolean;
  onDelete: (id: string) => void;
}

const BADGE: Record<Priority, string> = {
  info: "bg-status-new-bg text-status-new-fg border-status-new-border",
  warning: "bg-status-stalled-amber-bg text-status-stalled-amber-fg border-status-stalled-amber-border",
  urgent: "bg-status-stalled-red-bg text-status-stalled-red-fg border-status-stalled-red-border",
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export default function ShiftNoteCard({ note, canDelete, onDelete }: ShiftNoteCardProps) {
  return (
    <li className="rounded-md border border-border bg-surface shadow-sm p-4 space-y-2 transition-[box-shadow,border-color] duration-200 hover:shadow-md hover:border-border-strong">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          {note.priority && (
            <span
              className={`shrink-0 rounded-full border px-2 py-0.5 text-[11px] font-semibold capitalize ${BADGE[note.priority]}`}
            >
              {note.priority}
            </span>
          )}
          <p className="truncate text-[13px] font-semibold text-fg">
            {note.title}
          </p>
        </div>
        {canDelete && (
          <Button
            variant="danger"
            size="sm"
            onClick={() => onDelete(note.id)}
          >
            Delete
          </Button>
        )}
      </div>

      <p className="line-clamp-2 text-[13px] text-fg-muted leading-relaxed">
        {note.body}
      </p>

      <p className="text-[11px] text-fg-subtle">
        {note.author} · {formatDate(note.createdAt)}
      </p>
    </li>
  );
}
