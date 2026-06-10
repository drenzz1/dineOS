"use client";

import { useForm, type Resolver } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  staffMemberSchema,
  editStaffMemberSchema,
} from "@/lib/validations/staffMember";
import type { StaffMemberFormValues } from "@/lib/validations/staffMember";
import { saveStaffMember } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { Button } from "@/components/ui/Button";

const ROLES = ["Manager", "Cashier", "KitchenStaff"] as const;

interface StaffMemberFormProps {
  onClose: () => void;
  defaultValues?: Partial<StaffMemberFormValues> & { id?: number };
}

export default function StaffMemberForm({
  onClose,
  defaultValues,
}: StaffMemberFormProps) {
  const queryClient = useQueryClient();

  const isEdit = defaultValues?.id !== undefined;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StaffMemberFormValues>({
    // On create the PIN is required; on edit it is optional (blank keeps the
    // current PIN). Both schemas share the same field set, so the cast is safe.
    resolver: zodResolver(
      isEdit ? editStaffMemberSchema : staffMemberSchema
    ) as Resolver<StaffMemberFormValues>,
    defaultValues: {
      fullName: "",
      email: "",
      pin: "",
      ...defaultValues,
    },
  });

  const { mutate, isPending } = useMutation({
    mutationFn: (data: StaffMemberFormValues) =>
      saveStaffMember(data, defaultValues?.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.staff.all });
      onClose();
    },
  });

  return (
    <form
      onSubmit={handleSubmit((d) => mutate(d))}
      noValidate
      className="space-y-4"
    >
      {/* Full name */}
      <div className="space-y-1">
        <label
          htmlFor="sm-name"
          className="block text-sm font-medium text-zinc-700"
        >
          Full name
        </label>
        <input
          id="sm-name"
          type="text"
          {...register("fullName")}
          placeholder="e.g. Jane Doe"
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {errors.fullName && (
          <p className="text-sm text-red-600">{errors.fullName.message}</p>
        )}
      </div>

      {/* Email */}
      <div className="space-y-1">
        <label
          htmlFor="sm-email"
          className="block text-sm font-medium text-zinc-700"
        >
          Email
        </label>
        <input
          id="sm-email"
          type="email"
          {...register("email")}
          placeholder="jane@example.com"
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {errors.email && (
          <p className="text-sm text-red-600">{errors.email.message}</p>
        )}
      </div>

      {/* Role */}
      <div className="space-y-1">
        <label
          htmlFor="sm-role"
          className="block text-sm font-medium text-zinc-700"
        >
          Role
        </label>
        <select
          id="sm-role"
          {...register("role")}
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        >
          <option value="">Select a role</option>
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
        {errors.role && (
          <p className="text-sm text-red-600">{errors.role.message}</p>
        )}
      </div>

      {/* PIN */}
      <div className="space-y-1">
        <label
          htmlFor="sm-pin"
          className="block text-sm font-medium text-zinc-700"
        >
          {isEdit ? "New PIN (optional)" : "4-digit PIN"}
        </label>
        <input
          id="sm-pin"
          type="text"
          inputMode="numeric"
          maxLength={4}
          autoComplete="off"
          {...register("pin")}
          placeholder="••••"
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        <p className="text-xs text-zinc-500">
          {isEdit
            ? "Leave blank to keep the current PIN."
            : "The staff member types this PIN to start a shift on a shared terminal."}
        </p>
        {errors.pin && (
          <p className="text-sm text-red-600">{errors.pin.message}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 border-t border-zinc-200 pt-4">
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
