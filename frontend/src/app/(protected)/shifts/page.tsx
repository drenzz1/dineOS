"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Skeleton";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";
import { ShiftForm, ShiftCard, ShiftNoteForm, ShiftNoteList } from "@/components/shifts";
import {
  getShifts,
  deleteShift,
  getShiftNotes,
  deleteShiftNote,
} from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { useAuthStore } from "@/stores/authStore";
import type { Shift } from "@/types";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

function ShiftSkeleton() {
  return (
    <ul className="space-y-3">
      {[0, 1, 2].map((i) => (
        <li
          key={i}
          className="rounded-md border border-border bg-surface shadow-sm p-4 space-y-2"
        >
          <div className="flex items-center justify-between gap-3">
            <div className="space-y-1.5 flex-1">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-3 w-56" />
            </div>
            <div className="flex gap-2">
              <Skeleton className="h-7 w-12 rounded-md" />
              <Skeleton className="h-7 w-14 rounded-md" />
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}

function NoteSkeleton() {
  return (
    <ul className="space-y-3">
      {[0, 1].map((i) => (
        <li
          key={i}
          className="rounded-md border border-border bg-surface shadow-sm p-4 space-y-2"
        >
          <div className="flex items-center gap-2">
            <Skeleton className="h-5 w-14 rounded-full" />
            <Skeleton className="h-4 w-36" />
          </div>
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-2/3" />
          <Skeleton className="h-3 w-28" />
        </li>
      ))}
    </ul>
  );
}

function todayDateString(): string {
  return new Date().toISOString().split("T")[0] ?? "";
}

export default function ShiftsPage() {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const role = useAuthStore((s) => s.role);
  const isManager = role === "Manager" || role === "SuperAdmin";

  const [selectedDate, setSelectedDate] = useState(todayDateString);

  // ── Shift modal state ──────────────────────────────────────────────────────
  const [shiftModalOpen, setShiftModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Shift | undefined>(undefined);
  const [confirmDeleteShiftId, setConfirmDeleteShiftId] = useState<string | null>(null);

  // ── Note modal state ───────────────────────────────────────────────────────
  const [noteModalOpen, setNoteModalOpen] = useState(false);
  const [confirmDeleteNoteId, setConfirmDeleteNoteId] = useState<string | null>(null);

  // ── Shifts data ────────────────────────────────────────────────────────────
  const { data: shifts = [], isLoading: shiftsLoading } = useQuery({
    queryKey: queryKeys.shifts.list(tenantId, selectedDate),
    queryFn: () => getShifts(selectedDate),
  });

  const { mutate: doDeleteShift, isPending: isDeletingShift } = useMutation({
    mutationFn: deleteShift,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shifts.list(tenantId) });
      setConfirmDeleteShiftId(null);
    },
  });

  // ── Notes data ─────────────────────────────────────────────────────────────
  const { data: notes = [], isLoading: notesLoading } = useQuery({
    queryKey: queryKeys.shiftNotes.list(tenantId),
    queryFn: getShiftNotes,
  });

  const sortedNotes = [...notes].sort((a, b) =>
    b.createdAt.localeCompare(a.createdAt)
  );

  const { mutate: doDeleteNote, isPending: isDeletingNote } = useMutation({
    mutationFn: deleteShiftNote,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shiftNotes.list(tenantId) });
      setConfirmDeleteNoteId(null);
    },
  });

  function openCreateShift() {
    setEditTarget(undefined);
    setShiftModalOpen(true);
  }

  function openEditShift(shift: Shift) {
    setEditTarget(shift);
    setShiftModalOpen(true);
  }

  function closeShiftModal() {
    setShiftModalOpen(false);
    setEditTarget(undefined);
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div>
        <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">Shifts</h1>
        <p className="text-[13px] text-fg-muted mt-0.5">
          Manage the schedule and hand off context to the next shift.
        </p>
      </div>

      {/* Two-panel layout */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">

        {/* ── Left: shift schedule ──────────────────────────────────────── */}
        <div className="lg:col-span-2 space-y-4">
          <div className="flex items-center justify-between gap-4">
            <h2 className="text-[15px] font-semibold text-fg">Schedule</h2>
            <div className="flex items-center gap-3">
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                className="rounded-md border border-border bg-surface px-3 py-1.5 text-[13px] text-fg focus:border-border-strong focus:outline-none"
              />
              {isManager && (
                <Button
                  size="sm"
                  onClick={openCreateShift}
                  leading={
                    <svg
                      width="12"
                      height="12"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      aria-hidden="true"
                    >
                      <path d="M12 5v14M5 12h14" />
                    </svg>
                  }
                >
                  Add shift
                </Button>
              )}
            </div>
          </div>

          {shiftsLoading ? (
            <ShiftSkeleton />
          ) : shifts.length === 0 ? (
            <div className="rounded-md border border-dashed border-border-strong bg-surface">
              <EmptyState
                illustration={<Illo.Note />}
                title="No shifts on this date"
                description="Assign staff shifts to keep the schedule organised."
              />
            </div>
          ) : (
            <ul className="space-y-3">
              {shifts.map((shift) => (
                <ShiftCard
                  key={shift.id}
                  shift={shift}
                  canEdit={isManager}
                  onEdit={openEditShift}
                  onDelete={(id) => setConfirmDeleteShiftId(id)}
                />
              ))}
            </ul>
          )}
        </div>

        {/* ── Right: shift notes panel ──────────────────────────────────── */}
        <div className="space-y-4">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-[15px] font-semibold text-fg">Shift Notes</h2>
            {isManager && (
              <Button
                size="sm"
                variant="secondary"
                onClick={() => setNoteModalOpen(true)}
                leading={
                  <svg
                    width="12"
                    height="12"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                  >
                    <path d="M12 5v14M5 12h14" />
                  </svg>
                }
              >
                Add note
              </Button>
            )}
          </div>

          {notesLoading ? (
            <NoteSkeleton />
          ) : (
            <ShiftNoteList
              notes={sortedNotes}
              canDelete={isManager}
              onDelete={(id) => setConfirmDeleteNoteId(id)}
            />
          )}
        </div>
      </div>

      {/* ── Modals ──────────────────────────────────────────────────────────── */}

      {/* Create / edit shift */}
      <Modal
        isOpen={shiftModalOpen}
        onClose={closeShiftModal}
        title={editTarget ? "Edit shift" : "Add shift"}
      >
        <ShiftForm
          editTarget={editTarget}
          selectedDate={selectedDate}
          onClose={closeShiftModal}
        />
      </Modal>

      {/* Delete shift confirm */}
      <Modal
        isOpen={confirmDeleteShiftId !== null}
        onClose={() => setConfirmDeleteShiftId(null)}
        title="Delete shift?"
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setConfirmDeleteShiftId(null)}
              disabled={isDeletingShift}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isDeletingShift}
              onClick={() =>
                confirmDeleteShiftId && doDeleteShift(confirmDeleteShiftId)
              }
            >
              Delete
            </Button>
          </>
        }
      >
        <p className="text-[13px] text-fg-muted">This action cannot be undone.</p>
      </Modal>

      {/* Add shift note */}
      <Modal
        isOpen={noteModalOpen}
        onClose={() => setNoteModalOpen(false)}
        title="Add shift note"
      >
        <ShiftNoteForm onClose={() => setNoteModalOpen(false)} />
      </Modal>

      {/* Delete note confirm */}
      <Modal
        isOpen={confirmDeleteNoteId !== null}
        onClose={() => setConfirmDeleteNoteId(null)}
        title="Delete shift note?"
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setConfirmDeleteNoteId(null)}
              disabled={isDeletingNote}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isDeletingNote}
              onClick={() =>
                confirmDeleteNoteId && doDeleteNote(confirmDeleteNoteId)
              }
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
