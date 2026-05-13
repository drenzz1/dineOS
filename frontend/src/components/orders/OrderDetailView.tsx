"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Skeleton } from "@/components/ui/Skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getOrder, updateOrderStatus } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

const ALL_STATUSES: OrderStatus[] = [
  OrderStatus.New,
  OrderStatus.InProgress,
  OrderStatus.Ready,
  OrderStatus.Delivered,
  OrderStatus.Cancelled,
];

function money(value: number): string {
  return `$${value.toFixed(2)}`;
}

function orderTotal(order: Order): number {
  return order.items.reduce(
    (sum, item) => sum + item.quantity * item.unitPrice,
    0
  );
}

function orderLabel(order: Order): string {
  return order.orderType === "dine-in"
    ? `Dine-in${order.tableNumber != null ? ` · Table ${order.tableNumber}` : ""}`
    : "Pick-up";
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function DetailSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
      <section className="rounded-lg border border-border bg-surface p-5">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="mt-5 h-16 w-full" />
        <Skeleton className="mt-3 h-16 w-full" />
      </section>
      <aside className="rounded-lg border border-border bg-surface p-5">
        <Skeleton className="h-5 w-28" />
        <Skeleton className="mt-5 h-10 w-full" />
        <Skeleton className="mt-3 h-10 w-full" />
      </aside>
    </div>
  );
}

export default function OrderDetailView({ orderId }: { orderId: string }) {
  const queryClient = useQueryClient();

  const {
    data: order,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: queryKeys.orders.detail(orderId),
    queryFn: () => getOrder(orderId),
  });

  const { mutate: changeStatus, isPending } = useMutation({
    mutationFn: (status: OrderStatus) => updateOrderStatus(orderId, status),
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKeys.orders.detail(orderId), updated);
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    },
  });

  if (isLoading) {
    return <DetailSkeleton />;
  }

  if (isError || !order) {
    return (
      <div className="rounded-lg border border-border bg-surface py-10">
        <EmptyState
          title="Could not load order"
          description={error instanceof Error ? error.message : "Refresh and try again."}
        />
      </div>
    );
  }

  const availableStatuses = ALL_STATUSES.filter((status) => status !== order.status);
  const total = orderTotal(order);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link
            href="/orders"
            className="text-sm font-medium text-fg-muted transition-colors hover:text-fg"
          >
            Back to orders
          </Link>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-[-0.02em] text-fg">
              Order #{order.id}
            </h1>
            <StatusBadge status={order.status} />
          </div>
          <p className="mt-1 text-sm text-fg-muted">{orderLabel(order)}</p>
        </div>
        <Link href="/orders/new">
          <Button variant="secondary">New order</Button>
        </Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <section className="rounded-lg border border-border bg-surface shadow-sm">
          <div className="border-b border-border px-5 py-4">
            <h2 className="text-base font-semibold text-fg">Items</h2>
          </div>
          <div className="divide-y divide-border">
            {order.items.map((item) => (
              <div
                key={item.id}
                className="grid gap-3 px-5 py-4 sm:grid-cols-[1fr_auto] sm:items-center"
              >
                <div>
                  <p className="text-sm font-semibold text-fg">{item.name}</p>
                  <p className="text-xs text-fg-muted">
                    {item.quantity} x {money(item.unitPrice)}
                  </p>
                </div>
                <p className="font-mono text-sm font-semibold text-fg">
                  {money(item.quantity * item.unitPrice)}
                </p>
              </div>
            ))}
          </div>
          <div className="flex items-center justify-between border-t border-border px-5 py-4">
            <span className="text-sm font-semibold text-fg">Total</span>
            <span className="font-mono text-base font-semibold text-fg">
              {money(total)}
            </span>
          </div>
        </section>

        <aside className="space-y-4">
          <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
            <h2 className="text-base font-semibold text-fg">Status</h2>
            <div className="mt-4 grid gap-2">
              {availableStatuses.map((status) => (
                <Button
                  key={status}
                  type="button"
                  variant={status === OrderStatus.Cancelled ? "danger" : "primary"}
                  isLoading={isPending}
                  block
                  onClick={() => changeStatus(status)}
                >
                  {status === OrderStatus.Cancelled ? "Cancel order" : `Move to ${status}`}
                </Button>
              ))}
            </div>
          </section>

          <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
            <h2 className="text-base font-semibold text-fg">Timeline</h2>
            <dl className="mt-4 space-y-3 text-sm">
              <div>
                <dt className="text-fg-muted">Created</dt>
                <dd className="font-medium text-fg">{formatDateTime(order.createdAt)}</dd>
              </div>
              <div>
                <dt className="text-fg-muted">Last updated</dt>
                <dd className="font-medium text-fg">{formatDateTime(order.updatedAt)}</dd>
              </div>
            </dl>
          </section>

          {order.notes && (
            <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
              <h2 className="text-base font-semibold text-fg">Notes</h2>
              <p className="mt-3 text-sm text-fg-muted">{order.notes}</p>
            </section>
          )}
        </aside>
      </div>
    </div>
  );
}
