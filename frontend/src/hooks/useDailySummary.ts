import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { getOrders } from "@/lib/api/ordersApi";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

// ─── Types ────────────────────────────────────────────────────────────────────

export interface DailySummary {
  totalOrders: number;
  totalRevenue: number;
  cancelledOrders: number;
  avgPrepTimeMinutes: number;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function computeSummary(orders: Order[]): DailySummary {
  const delivered = orders.filter((o) => o.status === OrderStatus.Delivered);

  const totalRevenue = delivered.reduce((sum, o) => sum + o.total, 0);

  const cancelledOrders = orders.filter(
    (o) => o.status === OrderStatus.Cancelled
  ).length;

  const avgPrepTimeMinutes =
    delivered.length === 0
      ? 0
      : Math.round(
          delivered.reduce(
            (sum, o) =>
              sum +
              (new Date(o.updatedAt).getTime() -
                new Date(o.createdAt).getTime()),
            0
          ) /
            delivered.length /
            60_000
        );

  return {
    totalOrders: orders.length,
    totalRevenue,
    cancelledOrders,
    avgPrepTimeMinutes,
  };
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export interface UseDailySummaryResult {
  orders: Order[];
  summary: DailySummary;
  isLoading: boolean;
  isError: boolean;
}

export function useDailySummary(date: string): UseDailySummaryResult {
  const { tenantId } = useTenant();
  const { data: orders = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.orders.byDate(tenantId, date),
    queryFn: () => getOrders({ date }),
  });

  return { orders, summary: computeSummary(orders), isLoading, isError };
}
