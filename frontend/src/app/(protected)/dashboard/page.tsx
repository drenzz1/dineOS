"use client";

import { useState } from "react";
import { SummaryCards, SummaryCardsSkeleton } from "@/components/dashboard/SummaryCards";
import { OrdersTable, OrdersTableSkeleton } from "@/components/dashboard/OrdersTable";
import { useDailySummary } from "@/hooks/useDailySummary";

function todayString(): string {
  return new Date().toISOString().split("T")[0];
}

export default function DashboardPage() {
  const [date, setDate] = useState<string>(todayString);

  const { orders, summary, isLoading, isError } = useDailySummary(date);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
            Daily Summary
          </h1>
          <p className="text-[13px] text-fg-muted mt-0.5">
            Orders and revenue for the selected date.
          </p>
        </div>
        <label htmlFor="dashboard-date" className="sr-only">
          Summary date
        </label>
        <input
          id="dashboard-date"
          type="date"
          value={date}
          max={todayString()}
          onChange={(e) => {
            if (e.target.value) setDate(e.target.value);
          }}
          className="w-full sm:w-auto h-[34px] rounded-sm border border-border-strong bg-surface px-3 text-[13px] text-fg transition-[border-color,box-shadow] duration-150 focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-accent/25"
        />
      </div>

      {/* Error state */}
      {isError && (
        <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
          <p className="text-[13px] text-status-cancelled-fg">
            Failed to load data. Please try again.
          </p>
        </div>
      )}

      {/* Summary cards */}
      {isLoading ? (
        <SummaryCardsSkeleton />
      ) : (
        <SummaryCards summary={summary} />
      )}

      {/* Orders table */}
      {isLoading ? (
        <OrdersTableSkeleton />
      ) : (
        <OrdersTable orders={orders} />
      )}
    </div>
  );
}
