"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import type { UseFormRegisterReturn } from "react-hook-form";
import { useTenant } from "@/hooks/useTenant";
import { listRestaurantTables } from "@/lib/api/restaurantTablesApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { RestaurantTable } from "@/types/restaurantTable";

interface RestaurantTableSelectProps {
  registration: UseFormRegisterReturn<"tableNumber">;
  disabled?: boolean;
  error?: string;
  id?: string;
  label?: string;
}

function tableLabel(table: RestaurantTable): string {
  const details = [
    table.location?.trim() || null,
    `${table.capacity} ${table.capacity === 1 ? "seat" : "seats"}`,
    table.isActive ? null : "inactive",
  ].filter(Boolean);

  return `Table ${table.number}${details.length > 0 ? ` · ${details.join(" · ")}` : ""}`;
}

export default function RestaurantTableSelect({
  registration,
  disabled = false,
  error,
  id = "tableNumber",
  label = "Table",
}: RestaurantTableSelectProps) {
  const { tenantId } = useTenant();
  const {
    data: tables = [],
    isLoading,
    isError,
  } = useQuery({
    queryKey: queryKeys.restaurantTables.list(tenantId),
    queryFn: listRestaurantTables,
  });

  const sortedTables = useMemo(
    () => [...tables].sort((a, b) => a.number - b.number),
    [tables]
  );
  const hasActiveTables = sortedTables.some((table) => table.isActive);
  const unavailable = isLoading || isError || !hasActiveTables;
  const messageId = `${id}-message`;

  let placeholder = "Select a table";
  let helper: string | null = null;

  if (disabled) {
    placeholder = "Not needed for pickup";
  } else if (isLoading) {
    placeholder = "Loading tables...";
  } else if (isError) {
    placeholder = "Could not load tables";
    helper = "Refresh the page to try loading the table list again.";
  } else if (sortedTables.length === 0) {
    placeholder = "No tables configured";
    helper = "Add tables in Settings before creating a dine-in order.";
  } else if (!hasActiveTables) {
    placeholder = "No active tables available";
    helper = "Activate a table in Settings before creating a dine-in order.";
  }

  return (
    <div>
      <label htmlFor={id} className="text-xs font-semibold text-fg-muted">
        {label}
      </label>
      <select
        id={id}
        {...registration}
        disabled={disabled || unavailable}
        aria-invalid={error ? "true" : undefined}
        aria-describedby={error || helper ? messageId : undefined}
        aria-busy={isLoading}
        className="mt-1 h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-fg outline-none transition focus:border-accent disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-fg-subtle"
      >
        <option value="">{placeholder}</option>
        {sortedTables.map((table) => (
          <option
            key={table.id}
            value={table.number}
            disabled={!table.isActive}
          >
            {tableLabel(table)}
          </option>
        ))}
      </select>
      {(error || helper) && (
        <p
          id={messageId}
          className={`mt-1 text-xs ${error ? "text-danger" : "text-fg-subtle"}`}
        >
          {error ?? helper}
        </p>
      )}
    </div>
  );
}
