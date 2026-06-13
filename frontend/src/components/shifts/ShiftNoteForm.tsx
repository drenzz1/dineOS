"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { shiftNoteSchema } from "@/lib/validations/shiftNote";
import type { ShiftNoteFormValues } from "@/lib/validations/shiftNote";
import { saveShiftNote } from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { useMe } from "@/hooks/useMe";
import { Button } from "@/components/ui/Button";

const PRIORITIES = [
  { value: "info", label: "Info" },
  { value: "warning", label: "Warning" },
  { value: "urgent", label: "Urgent" },
] as const;

interface ShiftNoteFormProps {
  onClose: () => void;
}

export default function ShiftNoteForm({ onClose }: ShiftNoteFormProps) {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const { user } = useMe();

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<ShiftNoteFormValues>({
    resolver: zodResolver(shiftNoteSchema),
    defaultValues: {
      title: "",
      body: "",
    },
  });

  const { mutate, isPending } = useMutation({
    mutationFn: (data: ShiftNoteFormValues) =>
      saveShiftNote(data, user?.name ?? user?.username ?? ""),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.shiftNotes.list(tenantId) });
      onClose();
    },
  });

  const bodyLength = watch("body")?.length ?? 0;

  return (
    <form
      onSubmit={handleSubmit((d) => mutate(d))}
      noValidate
      className="space-y-4"
    >
      {/* Title */}
      <div className="space-y-1">
        <label
          htmlFor="sn-title"
          className="block text-sm font-medium text-fg-muted"
        >
          Title
        </label>
        <input
          id="sn-title"
          type="text"
          {...register("title")}
          placeholder="e.g. End of shift handover"
          className="block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        {errors.title && (
          <p className="text-sm text-danger">{errors.title.message}</p>
        )}
      </div>

      {/* Body */}
      <div className="space-y-1">
        <label
          htmlFor="sn-body"
          className="block text-sm font-medium text-fg-muted"
        >
          Note
        </label>
        <textarea
          id="sn-body"
          rows={5}
          {...register("body")}
          placeholder="Describe what happened during the shift..."
          className="block w-full resize-none rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        <div className="flex items-center justify-between">
          {errors.body ? (
            <p className="text-sm text-danger">{errors.body.message}</p>
          ) : (
            <span />
          )}
          <p className="text-xs text-fg-subtle">{bodyLength}/1000</p>
        </div>
      </div>

      {/* Priority */}
      <div className="space-y-1">
        <label
          htmlFor="sn-priority"
          className="block text-sm font-medium text-fg-muted"
        >
          Priority{" "}
          <span className="font-normal text-fg-subtle">(optional)</span>
        </label>
        <select
          id="sn-priority"
          {...register("priority", {
            setValueAs: (v: string) => (v === "" ? undefined : v),
          })}
          className="block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        >
          <option value="">No priority</option>
          {PRIORITIES.map(({ value, label }) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        {errors.priority && (
          <p className="text-sm text-danger">{errors.priority.message}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 border-t border-border pt-4">
        <Button type="button" variant="secondary" onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit" isLoading={isPending}>
          Save
        </Button>
      </div>
    </form>
  );
}
