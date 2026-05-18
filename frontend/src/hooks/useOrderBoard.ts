import { useQuery } from "@tanstack/react-query";
import { getOrders } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";
import { useOrderHub } from "@/lib/realtime/orderHub";

type GroupedOrders = Record<OrderStatus, Order[]>;

export interface OrderBoardFilters {
  date?: string;
  status?: OrderStatus | "all";
}

const EMPTY_GROUPS: GroupedOrders = {
  [OrderStatus.New]: [],
  [OrderStatus.InProgress]: [],
  [OrderStatus.Ready]: [],
  [OrderStatus.Delivered]: [],
  [OrderStatus.Cancelled]: [],
};

export function useOrderBoard(filters: OrderBoardFilters = {}) {
  const { tenantId } = useTenant();
  useOrderHub();
  const normalizedFilters = {
    date: filters.date,
    status: filters.status ?? "all",
  };
  const { data: orders = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.orders.list(tenantId, normalizedFilters),
    queryFn: () => getOrders(normalizedFilters),
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
