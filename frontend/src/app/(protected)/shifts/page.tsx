"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import { Button } from "@/components/ui/Button";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);
import { ShiftNoteForm, ShiftNoteList } from "@/components/shifts";
import { getShiftNotes, deleteShiftNote } from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";

export default function ShiftsPage() {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [open, setOpen] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  const { data = [] } = useQuery({
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
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Shifts</h1>
        {/* TODO: restrict to Manager role once #32 (auth/roles) is merged */}
        <Button onClick={() => setOpen(true)}>Add Shift Note</Button>
      </div>

      <ShiftNoteList notes={sorted} onDelete={(id) => setConfirmDeleteId(id)} />

      <Modal isOpen={open} onClose={() => setOpen(false)} title="Add Shift Note">
        <ShiftNoteForm onClose={() => setOpen(false)} />
      </Modal>

      <Modal
        isOpen={confirmDeleteId !== null}
        onClose={() => setConfirmDeleteId(null)}
        title="Delete shift note?"
      >
        <p className="text-sm text-zinc-600">This action cannot be undone.</p>
        <div className="flex justify-end gap-3 border-t border-zinc-200 pt-4 mt-4">
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
        </div>
      </Modal>
    </div>
  );
}
