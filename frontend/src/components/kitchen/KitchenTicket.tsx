"use client";

import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { updateOrderStatus } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatElapsed(dateStr: string): string {
  const minutes = Math.floor(
    (Date.now() - new Date(dateStr).getTime()) / 60_000
  );
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m ago`;
}

function orderTypeLabel(order: Order): string {
  return order.orderType === "dine-in"
    ? `Dine-in · Table ${order.tableNumber ?? "?"}`
    : "Pick-up";
}

// ─── Component ───────────────────────────────────────────────────────────────

interface KitchenTicketProps {
  order: Order;
}

export default function KitchenTicket({ order }: KitchenTicketProps) {
  const queryClient = useQueryClient();
  const [elapsed, setElapsed] = useState<string>(() =>
    formatElapsed(order.createdAt)
  );

  // Refresh elapsed time every 30 seconds client-side
  useEffect(() => {
    const id = setInterval(() => {
      setElapsed(formatElapsed(order.createdAt));
    }, 30_000);
    return () => clearInterval(id);
  }, [order.createdAt]);

  const { mutate: changeStatus, isPending } = useMutation({
    mutationFn: (status: OrderStatus) => updateOrderStatus(order.id, status),
    onSuccess: () => {
      // Invalidate all order queries so the board, order list, and daily
      // summary all stay in sync after a status change.
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });

  const borderClass =
    order.status === OrderStatus.New
      ? "border-blue-500"
      : "border-amber-400";

  return (
    <article
      className={`flex flex-col rounded-xl border-2 ${borderClass} bg-zinc-800 p-4 md:p-5`}
    >
      {/* Header row */}
      <div className="mb-4 flex items-start justify-between gap-2">
        <div>
          <p className="font-mono text-xs font-semibold uppercase tracking-wider text-zinc-400">
            #{order.id.slice(0, 8)}
          </p>
          <p className="mt-0.5 text-xl font-bold text-white md:text-2xl">
            {orderTypeLabel(order)}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1.5">
          <StatusBadge status={order.status} />
          <span className="text-sm text-zinc-400">{elapsed}</span>
        </div>
      </div>

      {/* Items list */}
      <ul className="mb-4 flex-1 space-y-2">
        {order.items.map((item) => (
          <li key={item.id} className="flex items-baseline gap-2">
            <span className="min-w-[1.5rem] text-right text-lg font-bold text-white">
              {item.quantity}×
            </span>
            <span className="text-base font-medium text-zinc-200">
              {item.name}
            </span>
          </li>
        ))}
      </ul>

      {/* Notes — highlighted amber when present */}
      {order.notes && (
        <div className="mb-4 rounded-lg border border-amber-400/30 bg-amber-400/10 px-3 py-2">
          <p className="text-sm font-semibold text-amber-300">
            ⚠ {order.notes}
          </p>
        </div>
      )}

      {/* Action button */}
      {order.status === OrderStatus.New && (
        <Button
          size="lg"
          className="w-full"
          isLoading={isPending}
          onClick={() => changeStatus(OrderStatus.InProgress)}
        >
          Start
        </Button>
      )}
      {order.status === OrderStatus.InProgress && (
        <Button
          size="lg"
          variant="secondary"
          className="w-full bg-green-700 text-white hover:bg-green-800"
          isLoading={isPending}
          onClick={() => changeStatus(OrderStatus.Ready)}
        >
          Mark Ready
        </Button>
      )}
    </article>
  );
}
