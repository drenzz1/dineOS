"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useTenant } from "@/hooks/useTenant";
import { useToast } from "@/hooks/useToast";
import { queryKeys } from "@/lib/api/queryKeys";
import {
  createRestaurantTable,
  listRestaurantTables,
  updateRestaurantTable,
} from "@/lib/api/restaurantTablesApi";
import {
  createRestaurantTableSchema,
  type CreateRestaurantTableFormValues,
} from "@/lib/validations/restaurantTable";
import { ApiError } from "@/lib/api/envelope";
import type { RestaurantTable } from "@/types/restaurantTable";

export default function RestaurantTablesPage() {
  const { tenantId } = useTenant();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: tables = [], isLoading } = useQuery({
    queryKey: queryKeys.restaurantTables.list(tenantId),
    queryFn: listRestaurantTables,
  });

  const [editingId, setEditingId] = useState<number | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateRestaurantTableFormValues>({
    resolver: zodResolver(createRestaurantTableSchema),
    defaultValues: { number: 1, capacity: 2, location: "" },
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: queryKeys.restaurantTables.all });

  const handleApiError = (err: unknown, fallback: string) => {
    const message = err instanceof ApiError ? err.error : fallback;
    toast({ title: fallback, description: message, variant: "error" });
  };

  const { mutate: addTable, isPending: isAdding } = useMutation({
    mutationFn: createRestaurantTable,
    onSuccess: () => {
      invalidate();
      reset({ number: 1, capacity: 2, location: "" });
      toast({ title: "Table added", variant: "success", testId: "tables-toast-success" });
    },
    onError: (err) => handleApiError(err, "Could not add table"),
  });

  const { mutate: toggleActive, isPending: isToggling } = useMutation({
    mutationFn: (table: RestaurantTable) =>
      updateRestaurantTable(table.id, { isActive: !table.isActive }),
    onSuccess: invalidate,
    onError: (err) => handleApiError(err, "Could not update table"),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
          Tables
        </h1>
        <p className="mt-0.5 text-[13px] text-fg-muted">
          Add, edit, and toggle the dining tables in your restaurant.
        </p>
      </div>

      <form
        data-testid="add-table-form"
        noValidate
        onSubmit={handleSubmit((values) => addTable(values))}
        className="grid grid-cols-1 gap-3 md:grid-cols-[1fr_1fr_2fr_auto] md:items-end bg-surface border border-border rounded-md p-4"
      >
        <Input
          id="table-number"
          label="Number"
          type="number"
          min={1}
          error={errors.number?.message}
          {...register("number", { valueAsNumber: true })}
        />
        <Input
          id="table-capacity"
          label="Capacity"
          type="number"
          min={1}
          max={50}
          error={errors.capacity?.message}
          {...register("capacity", { valueAsNumber: true })}
        />
        <Input
          id="table-location"
          label="Location (optional)"
          type="text"
          placeholder="Patio, Terrace, Window…"
          error={errors.location?.message}
          {...register("location")}
        />
        <Button type="submit" isLoading={isAdding}>
          Add table
        </Button>
      </form>

      {isLoading ? (
        <p className="text-[13px] text-fg-muted">Loading tables…</p>
      ) : tables.length === 0 ? (
        <p className="text-[13px] text-fg-muted">No tables yet. Add your first table above.</p>
      ) : (
        <div className="overflow-hidden bg-surface border border-border rounded-md">
          <table className="w-full text-[13px]">
            <thead className="bg-surface-2 text-fg-muted">
              <tr>
                <th className="text-left font-medium px-4 py-2">Number</th>
                <th className="text-left font-medium px-4 py-2">Capacity</th>
                <th className="text-left font-medium px-4 py-2">Location</th>
                <th className="text-left font-medium px-4 py-2">Active</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody>
              {tables.map((table) =>
                editingId === table.id ? (
                  <EditTableRow
                    key={table.id}
                    table={table}
                    onCancel={() => setEditingId(null)}
                    onSaved={() => {
                      setEditingId(null);
                      invalidate();
                    }}
                  />
                ) : (
                  <tr key={table.id} className="border-t border-border">
                    <td className="px-4 py-2 text-fg">#{table.number}</td>
                    <td className="px-4 py-2 text-fg">{table.capacity}</td>
                    <td className="px-4 py-2 text-fg-muted">{table.location ?? "—"}</td>
                    <td className="px-4 py-2">
                      <span
                        className={
                          table.isActive
                            ? "inline-flex items-center gap-1 text-status-ready-solid"
                            : "inline-flex items-center gap-1 text-fg-subtle"
                        }
                      >
                        <span
                          className={`h-1.5 w-1.5 rounded-full ${table.isActive ? "bg-status-ready-solid" : "bg-fg-subtle"}`}
                        />
                        {table.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-right space-x-2">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => setEditingId(table.id)}
                      >
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="secondary"
                        isLoading={isToggling}
                        onClick={() => toggleActive(table)}
                      >
                        {table.isActive ? "Deactivate" : "Activate"}
                      </Button>
                    </td>
                  </tr>
                )
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

interface EditTableRowProps {
  table: RestaurantTable;
  onCancel: () => void;
  onSaved: () => void;
}

function EditTableRow({ table, onCancel, onSaved }: EditTableRowProps) {
  const { toast } = useToast();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateRestaurantTableFormValues>({
    resolver: zodResolver(createRestaurantTableSchema),
    defaultValues: {
      number: table.number,
      capacity: table.capacity,
      location: table.location ?? "",
    },
  });

  const { mutate, isPending } = useMutation({
    mutationFn: (values: CreateRestaurantTableFormValues) =>
      updateRestaurantTable(table.id, values),
    onSuccess: () => {
      toast({ title: "Table updated", variant: "success" });
      onSaved();
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.error : "Could not update table";
      toast({ title: "Update failed", description: message, variant: "error" });
    },
  });

  return (
    <tr className="border-t border-border bg-surface-2/40">
      <td className="px-4 py-2">
        <Input
          id={`edit-number-${table.id}`}
          type="number"
          min={1}
          error={errors.number?.message}
          {...register("number", { valueAsNumber: true })}
        />
      </td>
      <td className="px-4 py-2">
        <Input
          id={`edit-capacity-${table.id}`}
          type="number"
          min={1}
          max={50}
          error={errors.capacity?.message}
          {...register("capacity", { valueAsNumber: true })}
        />
      </td>
      <td className="px-4 py-2" colSpan={2}>
        <Input
          id={`edit-location-${table.id}`}
          type="text"
          error={errors.location?.message}
          {...register("location")}
        />
      </td>
      <td className="px-4 py-2 text-right space-x-2 whitespace-nowrap">
        <Button size="sm" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          size="sm"
          isLoading={isPending}
          onClick={handleSubmit((values) => mutate(values))}
        >
          Save
        </Button>
      </td>
    </tr>
  );
}
