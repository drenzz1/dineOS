"use client";

import { useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateOrderStatus } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

interface OrderDetailPanelProps {
  order: Order | null;
  onClose: () => void;
}

const ALL_STATUSES: OrderStatus[] = [
  OrderStatus.New,
  OrderStatus.InProgress,
  OrderStatus.Ready,
  OrderStatus.Delivered,
  OrderStatus.Cancelled,
];

export default function OrderDetailPanel({ order, onClose }: OrderDetailPanelProps) {
  const queryClient = useQueryClient();

  // Close on Escape key
  useEffect(() => {
    if (!order) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [order, onClose]);

  const { mutate: changeStatus, isPending } = useMutation({
    mutationFn: ({
      orderId,
      status,
    }: {
      orderId: string;
      status: OrderStatus;
    }) => updateOrderStatus(orderId, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.list() });
      onClose();
    },
  });

  if (!order) return null;

  const total = order.items.reduce(
    (sum, item) => sum + item.unitPrice * item.quantity,
    0
  );

  const availableStatuses = ALL_STATUSES.filter((s) => s !== order.status);

  return (
    <>
      {/* Backdrop — closes panel on outside click */}
      <div
        className="fixed inset-0 z-40 bg-black/30"
        aria-hidden="true"
        onClick={onClose}
      />

      {/* Slide-in panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Order details"
        className="fixed inset-y-0 right-0 z-50 flex w-full max-w-sm flex-col bg-white shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-start justify-between border-b border-zinc-200 px-5 py-4">
          <div className="space-y-0.5">
            <p className="font-mono text-xs font-semibold uppercase text-zinc-400">
              #{order.id.slice(0, 8)}
            </p>
            <h2 className="text-base font-semibold text-zinc-900">
              {order.orderType === "dine-in"
                ? `Dine-in${order.tableNumber != null ? ` · Table ${order.tableNumber}` : ""}`
                : "Pick-up"}
            </h2>
            <StatusBadge status={order.status} />
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="mt-0.5 flex h-8 w-8 items-center justify-center rounded-md text-zinc-400 transition-colors hover:bg-zinc-100 hover:text-zinc-600"
          >
            ✕
          </button>
        </div>

        {/* Scrollable body */}
        <div className="flex-1 space-y-6 overflow-y-auto p-5">
          {/* Items */}
          <section>
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-400">
              Items
            </h3>
            <ul className="space-y-2">
              {order.items.map((item) => (
                <li
                  key={item.id}
                  className="flex items-center justify-between text-sm"
                >
                  <span className="text-zinc-700">
                    <span className="font-medium">{item.quantity}×</span>{" "}
                    {item.name}
                  </span>
                  <span className="text-zinc-500">
                    ${(item.unitPrice * item.quantity).toFixed(2)}
                  </span>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex justify-between border-t border-zinc-100 pt-2 text-sm font-semibold text-zinc-900">
              <span>Total</span>
              <span>${total.toFixed(2)}</span>
            </div>
          </section>

          {/* Notes */}
          {order.notes && (
            <section>
              <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-400">
                Notes
              </h3>
              <p className="text-sm text-zinc-600">{order.notes}</p>
            </section>
          )}

          {/* Status update */}
          <section>
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-400">
              Move to
            </h3>
            <div className="flex flex-wrap gap-2">
              {availableStatuses.map((status) => (
                <Button
                  key={status}
                  size="sm"
                  variant="secondary"
                  isLoading={isPending}
                  onClick={() => changeStatus({ orderId: order.id, status })}
                >
                  {status}
                </Button>
              ))}
            </div>
          </section>
        </div>
      </div>
    </>
  );
}
