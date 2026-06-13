"use client";

import { useEffect } from "react";
import { useFocusTrap } from "@/hooks/useFocusTrap";
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
  const panelRef = useFocusTrap(!!order);

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
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
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
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="Order details"
        className="fixed inset-y-0 right-0 z-50 flex w-full max-w-sm flex-col bg-surface shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-start justify-between border-b border-border px-5 py-4">
          <div className="space-y-0.5">
            <p className="font-mono text-xs font-semibold uppercase text-fg-subtle">
              #{order.id.slice(0, 8)}
            </p>
            <h2 className="text-base font-semibold text-fg">
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
            className="mt-0.5 flex h-8 w-8 items-center justify-center rounded-md text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg-muted"
          >
            ✕
          </button>
        </div>

        {/* Scrollable body */}
        <div className="flex-1 space-y-6 overflow-y-auto p-5">
          {/* Items */}
          <section>
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
              Items
            </h3>
            <ul className="space-y-2">
              {order.items.map((item) => (
                <li
                  key={item.id}
                  className="flex items-center justify-between text-sm"
                >
                  <span className="text-fg-muted">
                    <span className="font-medium">{item.quantity}×</span>{" "}
                    {item.name}
                  </span>
                  <span className="text-fg-subtle">
                    ${(item.unitPrice * item.quantity).toFixed(2)}
                  </span>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex justify-between border-t border-border pt-2 text-sm font-semibold text-fg">
              <span>Total</span>
              <span>${total.toFixed(2)}</span>
            </div>
          </section>

          {/* Notes */}
          {order.notes && (
            <section>
              <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
                Notes
              </h3>
              <p className="text-sm text-fg-muted">{order.notes}</p>
            </section>
          )}

          {/* Status update */}
          <section>
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
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
