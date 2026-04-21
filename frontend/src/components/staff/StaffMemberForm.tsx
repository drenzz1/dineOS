"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { staffMemberSchema } from "@/lib/validations/staffMember";
import type { StaffMemberFormValues } from "@/lib/validations/staffMember";
import { saveStaffMember } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { Button } from "@/components/ui/Button";

const ROLES = ["Manager", "Cashier", "KitchenStaff"] as const;

interface StaffMemberFormProps {
  onClose: () => void;
  defaultValues?: Partial<StaffMemberFormValues> & { id?: string };
}

export default function StaffMemberForm({
  onClose,
  defaultValues,
}: StaffMemberFormProps) {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<StaffMemberFormValues>({
    resolver: zodResolver(staffMemberSchema),
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
      queryClient.invalidateQueries({ queryKey: queryKeys.staff.list(tenantId) });
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
          PIN{" "}
          <span className="font-normal text-zinc-600">(4 digits)</span>
        </label>
        <input
          id="sm-pin"
          type="password"
          inputMode="numeric"
          maxLength={4}
          {...register("pin")}
          placeholder="••••"
          className="block w-24 rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 tracking-widest focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
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
