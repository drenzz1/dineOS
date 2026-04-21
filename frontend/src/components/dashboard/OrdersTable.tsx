"use client";

import { useState } from "react";
import { Card } from "@/components/ui/Card";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { OrderStatus } from "@/types/order";
import type { Order, OrderItem } from "@/types/order";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatTime(dateStr: string): string {
  return new Date(dateStr).toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatType(order: Order): string {
  return order.orderType === "dine-in"
    ? `Dine-in${order.tableNumber != null ? ` (T${order.tableNumber})` : ""}`
    : "Pick-up";
}

function formatItems(items: OrderItem[]): string {
  if (items.length === 0) return "—";
  const first = `${items[0].quantity}× ${items[0].name}`;
  return items.length > 1 ? `${first}, +${items.length - 1}` : first;
}

function orderTotal(items: OrderItem[]): number {
  return items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0);
}

// ─── Filter strip ─────────────────────────────────────────────────────────────

type StatusFilter = OrderStatus | "All";

const FILTER_OPTIONS: Array<{ label: string; value: StatusFilter }> = [
  { label: "All", value: "All" },
  { label: "New", value: OrderStatus.New },
  { label: "In Progress", value: OrderStatus.InProgress },
  { label: "Ready", value: OrderStatus.Ready },
  { label: "Delivered", value: OrderStatus.Delivered },
  { label: "Cancelled", value: OrderStatus.Cancelled },
];

interface FilterStripProps {
  active: StatusFilter;
  onChange: (value: StatusFilter) => void;
}

function FilterStrip({ active, onChange }: FilterStripProps) {
  return (
    <div className="flex gap-1.5 overflow-x-auto pb-1">
      {FILTER_OPTIONS.map(({ label, value }) => (
        <button
          key={value}
          type="button"
          onClick={() => onChange(value)}
          className={`shrink-0 rounded-full px-3 py-1 text-xs font-semibold transition-colors ${
            active === value
              ? "bg-zinc-900 text-white"
              : "bg-zinc-100 text-zinc-600 hover:bg-zinc-200"
          }`}
        >
          {label}
        </button>
      ))}
    </div>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

export function OrdersTableSkeleton() {
  return (
    <Card className="space-y-3">
      {/* Filter strip skeleton */}
      <div className="flex gap-2">
        {[80, 60, 90, 64, 80, 80].map((w, i) => (
          <div
            key={i}
            className="animate-pulse h-6 rounded-full bg-zinc-200"
            style={{ width: `${w}px` }}
          />
        ))}
      </div>
      {/* Rows skeleton */}
      <div className="animate-pulse space-y-2 pt-2">
        {[0, 1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className="flex items-center gap-4 rounded-md bg-zinc-100 px-3 py-3"
          >
            <div className="h-3 w-16 rounded bg-zinc-200" />
            <div className="h-3 w-20 rounded bg-zinc-200" />
            <div className="h-5 w-16 rounded-full bg-zinc-200" />
            <div className="h-3 flex-1 rounded bg-zinc-200" />
            <div className="h-3 w-10 rounded bg-zinc-200" />
            <div className="h-3 w-12 rounded bg-zinc-200" />
          </div>
        ))}
      </div>
    </Card>
  );
}

// ─── Table ────────────────────────────────────────────────────────────────────

interface OrdersTableProps {
  orders: Order[];
}

export function OrdersTable({ orders }: OrdersTableProps) {
  const [filter, setFilter] = useState<StatusFilter>("All");

  const visible =
    filter === "All" ? orders : orders.filter((o) => o.status === filter);

  return (
    <Card className="space-y-4">
      <FilterStrip active={filter} onChange={setFilter} />

      {visible.length === 0 ? (
        <div className="flex items-center justify-center rounded-lg border border-dashed border-zinc-300 py-12">
          <p className="text-sm text-zinc-600">
            {orders.length === 0
              ? "No orders for this date."
              : `No ${filter} orders.`}
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr className="border-b border-zinc-200 text-left">
                <th className="pb-2 pr-4 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Order #
                </th>
                <th className="pb-2 pr-4 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Type
                </th>
                <th className="pb-2 pr-4 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Status
                </th>
                <th className="pb-2 pr-4 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Items
                </th>
                <th className="pb-2 pr-4 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Total
                </th>
                <th className="pb-2 text-xs font-semibold uppercase tracking-wide text-zinc-600">
                  Time
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100">
              {visible.map((order) => (
                <tr key={order.id} className="hover:bg-zinc-50">
                  <td className="py-3 pr-4 font-mono text-xs font-semibold uppercase text-zinc-500">
                    #{order.id.slice(0, 8)}
                  </td>
                  <td className="py-3 pr-4 text-zinc-700">
                    {formatType(order)}
                  </td>
                  <td className="py-3 pr-4">
                    <StatusBadge status={order.status} />
                  </td>
                  <td className="py-3 pr-4 text-zinc-600">
                    {formatItems(order.items)}
                  </td>
                  <td className="py-3 pr-4 font-medium text-zinc-900">
                    ${orderTotal(order.items).toFixed(2)}
                  </td>
                  <td className="py-3 text-zinc-500">
                    {formatTime(order.createdAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}
