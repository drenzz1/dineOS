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
          <h1 className="text-2xl font-semibold text-zinc-900">
            Daily Summary
          </h1>
          <p className="mt-0.5 text-sm text-zinc-500">
            Overview of orders and revenue for the selected date.
          </p>
        </div>
        <input
          type="date"
          value={date}
          max={todayString()}
          onChange={(e) => {
            if (e.target.value) setDate(e.target.value);
          }}
          className="w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 sm:w-auto"
        />
      </div>

      {/* Error state */}
      {isError && (
        <div className="rounded-md bg-red-50 px-4 py-3">
          <p className="text-sm text-red-600">
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
