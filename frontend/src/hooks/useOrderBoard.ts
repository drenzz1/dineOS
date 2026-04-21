// TODO: replace with real API call and SignalR when backend is ready
import { useQuery } from "@tanstack/react-query";
import { getOrders } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

type GroupedOrders = Record<OrderStatus, Order[]>;

const EMPTY_GROUPS: GroupedOrders = {
  [OrderStatus.New]: [],
  [OrderStatus.InProgress]: [],
  [OrderStatus.Ready]: [],
  [OrderStatus.Delivered]: [],
  [OrderStatus.Cancelled]: [],
};

export function useOrderBoard() {
  const { tenantId } = useTenant();
  const { data: orders = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.orders.list(tenantId),
    queryFn: getOrders,
    // Poll every 30 s until SignalR real-time updates are wired up
    refetchInterval: 30_000,
  });

  const grouped: GroupedOrders = orders.reduce<GroupedOrders>(
    (acc, order) => {
      acc[order.status] = [...acc[order.status], order];
      return acc;
    },
    { ...EMPTY_GROUPS }
  );

  return { orders, grouped, isLoading, isError };
}
