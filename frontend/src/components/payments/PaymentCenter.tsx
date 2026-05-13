"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Skeleton } from "@/components/ui/Skeleton";
import { getOrders } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

type PaymentMethod = "Card" | "Cash";

function orderTotal(order: Order): number {
  return order.items.reduce(
    (sum, item) => sum + item.quantity * item.unitPrice,
    0
  );
}

function money(value: number): string {
  return `$${value.toFixed(2)}`;
}

function labelForOrder(order: Order): string {
  if (order.orderType === "dine-in" && order.tableNumber != null) {
    return `Table ${order.tableNumber}`;
  }

  return "Pickup";
}

export default function PaymentCenter() {
  const { tenantId } = useTenant();
  const [paidOrderIds, setPaidOrderIds] = useState<string[]>([]);
  const [methodsByOrderId, setMethodsByOrderId] = useState<Record<string, PaymentMethod>>({});

  const { data: orders = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.orders.payments(tenantId),
    queryFn: () => getOrders(),
  });

  const payableOrders = useMemo(
    () =>
      orders
        .filter((order) => order.status !== OrderStatus.Cancelled)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    [orders]
  );

  const openOrders = payableOrders.filter(
    (order) => !paidOrderIds.includes(order.id)
  );
  const paidOrders = payableOrders.filter((order) =>
    paidOrderIds.includes(order.id)
  );
  const openTotal = openOrders.reduce((sum, order) => sum + orderTotal(order), 0);

  function markPaid(orderId: string) {
    setPaidOrderIds((current) =>
      current.includes(orderId) ? current : [...current, orderId]
    );
  }

  function setMethod(orderId: string, method: PaymentMethod) {
    setMethodsByOrderId((current) => ({ ...current, [orderId]: method }));
  }

  if (isError) {
    return (
      <div className="rounded-lg border border-border bg-surface">
        <EmptyState
          title="Could not load payments"
          description="Refresh the page and try again."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-3">
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <p className="text-xs font-semibold text-fg-muted">Open checks</p>
          <p className="mt-1 font-mono text-3xl font-semibold tracking-[-0.03em] text-fg">
            {openOrders.length}
          </p>
        </div>
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <p className="text-xs font-semibold text-fg-muted">Amount due</p>
          <p className="mt-1 font-mono text-3xl font-semibold tracking-[-0.03em] text-fg">
            {money(openTotal)}
          </p>
        </div>
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <p className="text-xs font-semibold text-fg-muted">Paid this session</p>
          <p className="mt-1 font-mono text-3xl font-semibold tracking-[-0.03em] text-fg">
            {paidOrders.length}
          </p>
        </div>
      </div>

      <section className="rounded-lg border border-border bg-surface shadow-sm">
        <div className="border-b border-border px-4 py-3">
          <h2 className="text-base font-semibold text-fg">Open payments</h2>
          <p className="text-[12px] text-fg-muted">
            Select a method and mark the check as paid.
          </p>
        </div>

        {isLoading ? (
          <div className="space-y-3 p-4">
            {[0, 1, 2].map((index) => (
              <div key={index} className="rounded-md border border-border p-4">
                <Skeleton className="h-4 w-32" />
                <Skeleton className="mt-3 h-3 w-48" />
                <Skeleton className="mt-4 h-9 w-full" />
              </div>
            ))}
          </div>
        ) : openOrders.length === 0 ? (
          <EmptyState
            title="No open payments"
            description="Checks will appear here after an order is created."
          />
        ) : (
          <div className="divide-y divide-border">
            {openOrders.map((order) => {
              const method = methodsByOrderId[order.id] ?? "Card";
              return (
                <article key={order.id} className="grid gap-4 p-4 lg:grid-cols-[1fr_260px] lg:items-center">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="text-sm font-semibold text-fg">
                        {labelForOrder(order)} · #{order.id.slice(0, 8)}
                      </h3>
                      <span className="rounded-full bg-surface-2 px-2 py-0.5 text-[11px] font-semibold text-fg-muted">
                        {order.status}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-fg-muted">
                      {order.items.length} item{order.items.length === 1 ? "" : "s"} · {money(orderTotal(order))}
                    </p>
                    <div className="mt-3 flex flex-wrap gap-1.5">
                      {order.items.map((item) => (
                        <span
                          key={item.id}
                          className="rounded bg-bg-sunken px-2 py-1 text-[11px] text-fg-muted"
                        >
                          {item.quantity}x {item.name}
                        </span>
                      ))}
                    </div>
                  </div>

                  <div className="space-y-3">
                    <div className="grid grid-cols-2 gap-2">
                      {(["Card", "Cash"] as const).map((option) => (
                        <button
                          key={option}
                          type="button"
                          onClick={() => setMethod(order.id, option)}
                          className={`rounded-md border px-3 py-2 text-sm font-semibold transition ${
                            method === option
                              ? "border-accent bg-accent-soft text-accent"
                              : "border-border bg-surface text-fg-muted hover:text-fg"
                          }`}
                        >
                          {option}
                        </button>
                      ))}
                    </div>
                    <Button className="w-full" onClick={() => markPaid(order.id)}>
                      Mark paid · {money(orderTotal(order))}
                    </Button>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}
