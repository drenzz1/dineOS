"use client";

import { Card } from "@/components/ui/Card";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

interface OrderCardProps {
  order: Order;
  onClick: () => void;
}

function formatElapsed(dateStr: string): string {
  const diffMs = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m ago`;
}

function getAlertBorderClass(order: Order): string {
  if (order.status !== OrderStatus.InProgress) return "";
  const minutes = Math.floor(
    (Date.now() - new Date(order.updatedAt).getTime()) / 60_000
  );
  if (minutes > 20) return "border-2 border-red-500";
  if (minutes > 10) return "border-2 border-amber-400";
  return "";
}

export default function OrderCard({ order, onClick }: OrderCardProps) {
  const alertBorder = getAlertBorderClass(order);

  return (
    <Card
      onClick={onClick}
      className={`cursor-pointer transition-shadow hover:shadow-md ${alertBorder}`}
      role="button"
      tabIndex={0}
      data-testid="order-card"
      data-order-id={order.id}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick();
      }}
    >
      <div className="space-y-2">
        {/* Order ID + elapsed time */}
        <div className="flex items-center justify-between">
          <span className="font-mono text-xs font-semibold uppercase text-zinc-600">
            #{order.id.slice(0, 8)}
          </span>
          <span className="text-xs text-zinc-600">
            {formatElapsed(order.createdAt)}
          </span>
        </div>

        {/* Order type + table */}
        <p className="text-sm font-medium text-zinc-900">
          {order.orderType === "dine-in"
            ? `Dine-in${order.tableNumber != null ? ` · Table ${order.tableNumber}` : ""}`
            : "Pick-up"}
        </p>

        {/* Item count */}
        <p className="text-xs text-zinc-500">
          {order.items.length} item{order.items.length !== 1 ? "s" : ""}
        </p>

        {/* Note preview */}
        {order.notes && (
          <p className="flex items-start gap-1 text-xs text-zinc-600">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 16 16"
              fill="currentColor"
              className="mt-px h-3 w-3 shrink-0"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                d="M2 4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V4Zm2 1.5a.75.75 0 0 1 .75-.75h6.5a.75.75 0 0 1 0 1.5h-6.5A.75.75 0 0 1 4 5.5Zm0 3a.75.75 0 0 1 .75-.75h6.5a.75.75 0 0 1 0 1.5h-6.5A.75.75 0 0 1 4 8.5Zm0 3a.75.75 0 0 1 .75-.75h4a.75.75 0 0 1 0 1.5h-4A.75.75 0 0 1 4 11.5Z"
                clipRule="evenodd"
              />
            </svg>
            <span className="line-clamp-1">{order.notes}</span>
          </p>
        )}

        <StatusBadge status={order.status} data-testid="order-status-badge" />
      </div>
    </Card>
  );
}
