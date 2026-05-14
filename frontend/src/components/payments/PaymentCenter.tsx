"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Skeleton } from "@/components/ui/Skeleton";
import { getOpenOrders, processPayment } from "@/lib/api/paymentsApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { useToast } from "@/hooks/useToast";
import { ApiError } from "@/lib/api/envelope";
import type { Order } from "@/types/order";
import type { PaymentMethod } from "@/types/payment";

function money(value: number): string {
  return `$${value.toFixed(2)}`;
}

function labelForOrder(order: Order): string {
  if (order.orderType === "dine-in" && order.tableNumber != null) {
    return `Table ${order.tableNumber}`;
  }
  return "Pickup";
}

function toastForPaymentError(err: unknown): {
  title: string;
  description?: string;
  variant: "error" | "warning";
} {
  if (err instanceof ApiError) {
    const message = (err.errors[0] ?? err.error ?? "").toLowerCase();
    if (err.status === 404) {
      return {
        title: "Order no longer available",
        description: "It may have been cancelled or already settled.",
        variant: "warning",
      };
    }
    if (err.status === 422) {
      if (message.includes("does not match")) {
        return {
          title: "Amount mismatch",
          description: "The submitted amount does not match the order total.",
          variant: "error",
        };
      }
      if (message.includes("already")) {
        return {
          title: "Order already settled",
          description: "This check has already been paid or cancelled.",
          variant: "warning",
        };
      }
    }
  }
  const message = err instanceof Error ? err.message : "Please try again.";
  return { title: "Payment failed", description: message, variant: "error" };
}

export default function PaymentCenter() {
  const { tenantId } = useTenant();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [methodsByOrderId, setMethodsByOrderId] = useState<Record<string, PaymentMethod>>({});

  const {
    data: openOrders = [],
    isLoading,
    isError,
  } = useQuery({
    queryKey: queryKeys.payments.openOrders(tenantId),
    queryFn: getOpenOrders,
  });

  const sortedOpenOrders = useMemo(
    () => [...openOrders].sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    [openOrders]
  );

  const openTotal = sortedOpenOrders.reduce((sum, order) => sum + order.total, 0);

  const mutation = useMutation({
    mutationFn: processPayment,
    onSuccess: (payment) => {
      toast({
        title: `Order #${payment.orderId} settled`,
        description: `${payment.method} · ${money(payment.amount)}`,
        variant: "success",
      });
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.payments.all });
    },
    onError: (error, variables) => {
      const opts = toastForPaymentError(error);
      toast(opts);
      if (error instanceof ApiError && error.status === 404) {
        queryClient.invalidateQueries({
          queryKey: queryKeys.payments.openOrders(tenantId),
        });
      }
      void variables;
    },
  });

  function setMethod(orderId: string, method: PaymentMethod) {
    setMethodsByOrderId((current) => ({ ...current, [orderId]: method }));
  }

  function handleMarkPaid(order: Order) {
    const method = methodsByOrderId[order.id] ?? "Card";
    mutation.mutate({
      orderId: order.id,
      amount: order.total,
      method,
    });
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

  const pendingOrderId =
    mutation.isPending ? mutation.variables?.orderId ?? null : null;

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <p className="text-xs font-semibold text-fg-muted">Open checks</p>
          <p className="mt-1 font-mono text-3xl font-semibold tracking-[-0.03em] text-fg">
            {sortedOpenOrders.length}
          </p>
        </div>
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <p className="text-xs font-semibold text-fg-muted">Amount due</p>
          <p className="mt-1 font-mono text-3xl font-semibold tracking-[-0.03em] text-fg">
            {money(openTotal)}
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
        ) : sortedOpenOrders.length === 0 ? (
          <EmptyState
            title="No open payments"
            description="Checks will appear here after an order is created."
          />
        ) : (
          <div className="divide-y divide-border">
            {sortedOpenOrders.map((order) => {
              const method = methodsByOrderId[order.id] ?? "Card";
              const isPendingForThis = pendingOrderId === order.id;
              return (
                <article
                  key={order.id}
                  className="grid gap-4 p-4 lg:grid-cols-[1fr_260px] lg:items-center"
                >
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="text-sm font-semibold text-fg">
                        {labelForOrder(order)} · #{order.id}
                      </h3>
                      <span className="rounded-full bg-surface-2 px-2 py-0.5 text-[11px] font-semibold text-fg-muted">
                        {order.status}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-fg-muted">
                      {order.items.length} item
                      {order.items.length === 1 ? "" : "s"} · {money(order.total)}
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
                          disabled={isPendingForThis}
                          className={`rounded-md border px-3 py-2 text-sm font-semibold transition disabled:opacity-50 disabled:cursor-not-allowed ${
                            method === option
                              ? "border-accent bg-accent-soft text-accent"
                              : "border-border bg-surface text-fg-muted hover:text-fg"
                          }`}
                        >
                          {option}
                        </button>
                      ))}
                    </div>
                    <Button
                      className="w-full"
                      onClick={() => handleMarkPaid(order)}
                      disabled={mutation.isPending}
                    >
                      {isPendingForThis ? "Processing…" : `Mark paid · ${money(order.total)}`}
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
