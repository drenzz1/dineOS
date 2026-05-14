"use client";

import type { Shift } from "@/types";
import { Button } from "@/components/ui/Button";

interface ShiftCardProps {
  shift: Shift;
  canEdit: boolean;
  onEdit: (shift: Shift) => void;
  onDelete: (id: string) => void;
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString(undefined, { timeStyle: "short" });
}

export default function ShiftCard({ shift, canEdit, onEdit, onDelete }: ShiftCardProps) {
  return (
    <li className="rounded-md border border-border bg-surface shadow-sm p-4 transition-[box-shadow,border-color] duration-200 hover:shadow-md hover:border-border-strong">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[13px] font-semibold text-fg truncate">{shift.staffName}</p>
          <p className="text-[12px] text-fg-muted mt-0.5">
            {formatDateTime(shift.startTime)} – {formatTime(shift.endTime)}
          </p>
          {shift.notes && (
            <p className="text-[12px] text-fg-muted mt-1 line-clamp-2">{shift.notes}</p>
          )}
        </div>
        {canEdit && (
          <div className="flex items-center gap-2 shrink-0">
            <Button variant="secondary" size="sm" onClick={() => onEdit(shift)}>
              Edit
            </Button>
            <Button variant="danger" size="sm" onClick={() => onDelete(shift.id)}>
              Delete
            </Button>
          </div>
        )}
      </div>
    </li>
  );
}
