"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getOrders } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/StatusBadge";

export default function OrdersPage() {
  const { data: orders = [], isLoading } = useQuery({
    queryKey: queryKeys.orders.list(),
    queryFn: getOrders,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Orders</h1>
        <Link href="/orders/new">
          <Button>New Order</Button>
        </Link>
      </div>

      {isLoading ? (
        <p className="text-sm text-zinc-500">Loading orders...</p>
      ) : orders.length === 0 ? (
        <div className="rounded-lg border border-dashed border-zinc-300 p-12 text-center">
          <p className="text-sm text-zinc-500">
            No orders yet. Create your first order.
          </p>
          <Link href="/orders/new" className="mt-4 inline-block">
            <Button size="sm" variant="secondary">
              New Order
            </Button>
          </Link>
        </div>
      ) : (
        <div className="divide-y divide-zinc-200 rounded-lg border border-zinc-200">
          {orders.map((order) => {
            const total = order.items.reduce(
              (sum, i) => sum + i.unitPrice * i.quantity,
              0
            );
            return (
              <div
                key={order.id}
                className="flex items-center justify-between p-4"
              >
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-medium capitalize text-zinc-900">
                      {order.orderType}
                      {order.tableNumber != null &&
                        ` — Table ${order.tableNumber}`}
                    </p>
                    <StatusBadge status={order.status} />
                  </div>
                  <p className="text-xs text-zinc-500">
                    {order.items.length} item
                    {order.items.length !== 1 ? "s" : ""} ·{" "}
                    {order.items.map((i) => i.name).join(", ")}
                  </p>
                </div>
                <p className="text-sm font-medium text-zinc-700">
                  ${total.toFixed(2)}
                </p>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
