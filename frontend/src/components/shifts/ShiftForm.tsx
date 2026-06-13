"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { shiftSchema } from "@/lib/validations/shift";
import type { ShiftFormValues } from "@/lib/validations/shift";
import { createShift, updateShift } from "@/lib/api/shiftApi";
import { getStaff } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { Button } from "@/components/ui/Button";
import type { Shift } from "@/types";

interface ShiftFormProps {
  editTarget?: Shift;
  selectedDate?: string;
  onClose: () => void;
}

function toDatetimeLocal(iso: string): string {
  return iso.slice(0, 16);
}

export default function ShiftForm({ editTarget, selectedDate, onClose }: ShiftFormProps) {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();

  const defaultStart = selectedDate ? `${selectedDate}T09:00` : "";
  const defaultEnd = selectedDate ? `${selectedDate}T17:00` : "";

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ShiftFormValues>({
    resolver: zodResolver(shiftSchema),
    defaultValues: editTarget
      ? {
          staffMemberId: Number(editTarget.staffMemberId),
          startTime: toDatetimeLocal(editTarget.startTime),
          endTime: toDatetimeLocal(editTarget.endTime),
          notes: editTarget.notes ?? "",
        }
      : {
          staffMemberId: 0,
          startTime: defaultStart,
          endTime: defaultEnd,
          notes: "",
        },
  });

  useEffect(() => {
    if (editTarget) {
      reset({
        staffMemberId: Number(editTarget.staffMemberId),
        startTime: toDatetimeLocal(editTarget.startTime),
        endTime: toDatetimeLocal(editTarget.endTime),
        notes: editTarget.notes ?? "",
      });
    }
  }, [editTarget, reset]);

  const { data: staff = [] } = useQuery({
    queryKey: queryKeys.staff.list(tenantId),
    queryFn: getStaff,
  });

  const invalidateAndClose = () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.shifts.list(tenantId) });
    onClose();
  };

  const { mutate: create, isPending: isCreating } = useMutation({
    mutationFn: createShift,
    onSuccess: invalidateAndClose,
  });

  const { mutate: edit, isPending: isEditing } = useMutation({
    mutationFn: (data: ShiftFormValues) => updateShift(editTarget!.id, data),
    onSuccess: invalidateAndClose,
  });

  const isPending = isCreating || isEditing;

  return (
    <form
      onSubmit={handleSubmit((d) => (editTarget ? edit(d) : create(d)))}
      noValidate
      className="space-y-4"
    >
      {/* Staff member */}
      <div className="space-y-1">
        <label htmlFor="sf-staff" className="block text-sm font-medium text-fg-muted">
          Staff member
        </label>
        <select
          id="sf-staff"
          {...register("staffMemberId", { valueAsNumber: true })}
          className="block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        >
          <option value={0}>Select staff member…</option>
          {staff.map((s) => (
            <option key={s.id} value={s.id}>
              {s.fullName}
            </option>
          ))}
        </select>
        {errors.staffMemberId && (
          <p className="text-sm text-danger">{errors.staffMemberId.message}</p>
        )}
      </div>

      {/* Start time */}
      <div className="space-y-1">
        <label htmlFor="sf-start" className="block text-sm font-medium text-fg-muted">
          Start time
        </label>
        <input
          id="sf-start"
          type="datetime-local"
          {...register("startTime")}
          className="block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        {errors.startTime && (
          <p className="text-sm text-danger">{errors.startTime.message}</p>
        )}
      </div>

      {/* End time */}
      <div className="space-y-1">
        <label htmlFor="sf-end" className="block text-sm font-medium text-fg-muted">
          End time
        </label>
        <input
          id="sf-end"
          type="datetime-local"
          {...register("endTime")}
          className="block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        {errors.endTime && (
          <p className="text-sm text-danger">{errors.endTime.message}</p>
        )}
      </div>

      {/* Notes */}
      <div className="space-y-1">
        <label htmlFor="sf-notes" className="block text-sm font-medium text-fg-muted">
          Notes <span className="font-normal text-fg-subtle">(optional)</span>
        </label>
        <textarea
          id="sf-notes"
          rows={3}
          {...register("notes")}
          placeholder="Any notes for this shift…"
          className="block w-full resize-none rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        {errors.notes && (
          <p className="text-sm text-danger">{errors.notes.message}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 border-t border-border pt-4">
        <Button type="button" variant="secondary" onClick={onClose} disabled={isPending}>
          Cancel
        </Button>
        <Button type="submit" isLoading={isPending}>
          {editTarget ? "Save changes" : "Create shift"}
        </Button>
      </div>
    </form>
  );
}
