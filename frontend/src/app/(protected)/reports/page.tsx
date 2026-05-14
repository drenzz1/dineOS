"use client";

import { useState } from "react";
import dynamic from "next/dynamic";

const SalesTab = dynamic(() => import("@/components/reports/SalesTab"), { ssr: false });
const OrdersTab = dynamic(() => import("@/components/reports/OrdersTab"), { ssr: false });
const StaffTab = dynamic(() => import("@/components/reports/StaffTab"), { ssr: false });

type Tab = "sales" | "orders" | "staff";

const TABS: Array<{ id: Tab; label: string }> = [
  { id: "sales", label: "Sales" },
  { id: "orders", label: "Orders" },
  { id: "staff", label: "Staff" },
];

function defaultRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return {
    from: from.toISOString().split("T")[0]!,
    to: to.toISOString().split("T")[0]!,
  };
}

export default function ReportsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("sales");
  const [dateRange, setDateRange] = useState(defaultRange);

  const showDatePicker = activeTab !== "staff";

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">Reports</h1>
          <p className="text-[13px] text-fg-muted mt-0.5">Business insights and analytics.</p>
        </div>
        {showDatePicker && (
          <div className="flex items-center gap-2">
            <input
              type="date"
              value={dateRange.from}
              max={dateRange.to}
              onChange={(e) =>
                setDateRange((prev) => ({ ...prev, from: e.target.value }))
              }
              className="h-[34px] rounded-md border border-border bg-surface px-3 text-[13px] text-fg focus:outline-none focus:ring-1 focus:ring-accent"
            />
            <span className="text-[13px] text-fg-muted">to</span>
            <input
              type="date"
              value={dateRange.to}
              min={dateRange.from}
              onChange={(e) =>
                setDateRange((prev) => ({ ...prev, to: e.target.value }))
              }
              className="h-[34px] rounded-md border border-border bg-surface px-3 text-[13px] text-fg focus:outline-none focus:ring-1 focus:ring-accent"
            />
          </div>
        )}
      </div>

      <div className="flex gap-1.5">
        {TABS.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            onClick={() => setActiveTab(id)}
            aria-pressed={activeTab === id}
            className={`shrink-0 inline-flex items-center rounded-full border h-7 px-3 text-[12px] font-semibold transition-colors duration-150 ${
              activeTab === id
                ? "bg-accent text-accent-fg border-accent"
                : "bg-surface text-fg-muted border-border hover:bg-surface-2 hover:text-fg hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === "sales" && <SalesTab from={dateRange.from} to={dateRange.to} />}
      {activeTab === "orders" && <OrdersTab from={dateRange.from} to={dateRange.to} />}
      {activeTab === "staff" && <StaffTab />}
    </div>
  );
}
