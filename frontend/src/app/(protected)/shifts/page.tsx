"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Skeleton";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);
import { ShiftNoteForm, ShiftNoteList } from "@/components/shifts";
import { getShiftNotes, deleteShiftNote } from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";

function ShiftListSkeleton() {
  return (
    <ul className="space-y-3">
      {[0, 1, 2].map((i) => (
        <li
          key={i}
          className="rounded-md border border-border bg-surface shadow-sm p-4 space-y-2"
        >
          <div className="flex items-center gap-2">
            <Skeleton className="h-5 w-16 rounded-full" />
            <Skeleton className="h-4 w-40" />
          </div>
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-2/3" />
          <Skeleton className="h-3 w-32" />
        </li>
      ))}
    </ul>
  );
}

export default function ShiftsPage() {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [open, setOpen] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  const { data = [], isLoading } = useQuery({
    queryKey: queryKeys.shifts.list(tenantId),
    queryFn: getShiftNotes,
  });

  const sorted = [...data].sort(
    (a, b) => b.createdAt.localeCompare(a.createdAt)
  );

  const { mutate: doDelete, isPending: isDeleting } = useMutation({
    mutationFn: deleteShiftNote,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shifts.list(tenantId) });
      setConfirmDeleteId(null);
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
            Shift Notes
          </h1>
          <p className="text-[13px] text-fg-muted mt-0.5">
            Hand off context to the next shift — counts, VIPs, repairs, outages.
          </p>
        </div>
        <Button
          onClick={() => setOpen(true)}
          leading={
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 5v14M5 12h14" />
            </svg>
          }
        >
          Add Shift Note
        </Button>
      </div>

      {isLoading ? (
        <ShiftListSkeleton />
      ) : (
        <ShiftNoteList notes={sorted} onDelete={(id) => setConfirmDeleteId(id)} />
      )}

      <Modal isOpen={open} onClose={() => setOpen(false)} title="Add Shift Note">
        <ShiftNoteForm onClose={() => setOpen(false)} />
      </Modal>

      <Modal
        isOpen={confirmDeleteId !== null}
        onClose={() => setConfirmDeleteId(null)}
        title="Delete shift note?"
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setConfirmDeleteId(null)}
              disabled={isDeleting}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isDeleting}
              onClick={() => confirmDeleteId && doDelete(confirmDeleteId)}
            >
              Delete
            </Button>
          </>
        }
      >
        <p className="text-[13px] text-fg-muted">This action cannot be undone.</p>
      </Modal>
    </div>
  );
}
