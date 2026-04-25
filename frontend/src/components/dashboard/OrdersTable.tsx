"use client";

import { useState } from "react";
import { Card } from "@/components/ui/Card";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";
import { Skeleton } from "@/components/ui/Skeleton";
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
      {FILTER_OPTIONS.map(({ label, value }) => {
        const isActive = active === value;
        return (
          <button
            key={value}
            type="button"
            onClick={() => onChange(value)}
            aria-pressed={isActive}
            className={`shrink-0 inline-flex items-center rounded-full border h-7 px-3 text-[12px] font-semibold transition-colors duration-150 ${
              isActive
                ? "bg-accent text-accent-fg border-accent"
                : "bg-surface text-fg-muted border-border hover:bg-surface-2 hover:text-fg hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        );
      })}
    </div>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

export function OrdersTableSkeleton() {
  return (
    <Card className="space-y-4">
      <div className="flex gap-2">
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <Skeleton key={i} className="h-7 w-20 rounded-full" />
        ))}
      </div>
      <div className="space-y-2 pt-2">
        {[0, 1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className="grid grid-cols-[1fr_1fr_1fr_2fr_0.8fr_0.8fr] items-center gap-4 px-1 py-3 border-b border-border last:border-b-0"
          >
            <Skeleton className="h-3 w-16" />
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-5 w-16 rounded-full" />
            <Skeleton className="h-3 w-full" />
            <Skeleton className="h-3 w-12" />
            <Skeleton className="h-3 w-14" />
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
        <div className="rounded-md border border-dashed border-border-strong bg-surface">
          <EmptyState
            illustration={<Illo.Ticket />}
            title={orders.length === 0 ? "No orders for this date" : `No ${filter} orders`}
            description={
              orders.length === 0
                ? "When orders come in, they'll show up here for review."
                : "Try clearing the filter or selecting a different day."
            }
            compact
          />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-[13px]">
            <thead>
              <tr className="border-b border-border text-left">
                <th className="pb-2 pr-4 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Order #
                </th>
                <th className="pb-2 pr-4 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Type
                </th>
                <th className="pb-2 pr-4 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Status
                </th>
                <th className="pb-2 pr-4 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Items
                </th>
                <th className="pb-2 pr-4 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Total
                </th>
                <th className="pb-2 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
                  Time
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {visible.map((order) => (
                <tr
                  key={order.id}
                  className="transition-colors duration-150 hover:bg-surface-2"
                >
                  <td className="py-3 pr-4 dos-num text-[11.5px] font-semibold uppercase text-fg-muted">
                    #{order.id.slice(0, 8)}
                  </td>
                  <td className="py-3 pr-4 text-fg">{formatType(order)}</td>
                  <td className="py-3 pr-4">
                    <StatusBadge status={order.status} />
                  </td>
                  <td className="py-3 pr-4 text-fg-muted">
                    {formatItems(order.items)}
                  </td>
                  <td className="py-3 pr-4 dos-num font-medium text-fg">
                    ${orderTotal(order.items).toFixed(2)}
                  </td>
                  <td className="py-3 dos-num text-fg-subtle">
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
