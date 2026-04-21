"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/Button";
import OrderCard from "./OrderCard";
import OrderDetailPanel from "./OrderDetailPanel";
import { useOrderBoard } from "@/hooks/useOrderBoard";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

interface Column {
  status: OrderStatus;
  label: string;
  headerClass: string;
  emptyMessage: string;
}

const COLUMNS: Column[] = [
  {
    status: OrderStatus.New,
    label: "New",
    headerClass: "bg-blue-50 text-blue-700",
    emptyMessage: "No new orders",
  },
  {
    status: OrderStatus.InProgress,
    label: "In Progress",
    headerClass: "bg-amber-50 text-amber-700",
    emptyMessage: "Nothing in progress",
  },
  {
    status: OrderStatus.Ready,
    label: "Ready",
    headerClass: "bg-green-50 text-green-700",
    emptyMessage: "Nothing ready yet",
  },
  {
    status: OrderStatus.Delivered,
    label: "Delivered",
    headerClass: "bg-zinc-100 text-zinc-600",
    emptyMessage: "No delivered orders",
  },
  {
    status: OrderStatus.Cancelled,
    label: "Cancelled",
    headerClass: "bg-red-50 text-red-700",
    emptyMessage: "No cancelled orders",
  },
];

export default function OrderBoard() {
  const { grouped, isLoading, isError } = useOrderBoard();
  const [selectedOrder, setSelectedOrder] = useState<Order | null>(null);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-sm text-zinc-500">Loading orders...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex items-center justify-center py-24">
        <p className="text-sm text-red-500">
          Failed to load orders. Please refresh.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Order Board</h1>
        <Link href="/orders/new">
          <Button data-testid="new-order-button">New Order</Button>
        </Link>
      </div>

      {/* Board — single column on mobile, 5-column grid on lg+ */}
      <div data-testid="orders-list" className="flex flex-col gap-4 lg:grid lg:grid-cols-5 lg:items-start lg:gap-4">
        {COLUMNS.map(({ status, label, headerClass, emptyMessage }) => {
          const orders = grouped[status];

          return (
            <section key={status} className="flex flex-col gap-2">
              {/* Column header */}
              <div
                className={`flex items-center justify-between rounded-md px-3 py-2 ${headerClass}`}
              >
                <span className="text-sm font-semibold">{label}</span>
                <span className="min-w-[1.25rem] rounded-full bg-white/60 px-1.5 text-center text-xs font-bold">
                  {orders.length}
                </span>
              </div>

              {/* Order cards / empty state */}
              {orders.length === 0 ? (
                <p className="px-2 py-3 text-xs italic text-zinc-600">
                  {emptyMessage}
                </p>
              ) : (
                <div className="flex flex-col gap-2">
                  {orders.map((order) => (
                    <OrderCard
                      key={order.id}
                      order={order}
                      onClick={() => setSelectedOrder(order)}
                    />
                  ))}
                </div>
              )}
            </section>
          );
        })}
      </div>

      {/* Detail panel — slides in from right when an order is selected */}
      <OrderDetailPanel
        order={selectedOrder}
        onClose={() => setSelectedOrder(null)}
      />
    </div>
  );
}
