import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { OrderStatus } from "@/types/order";
import type { KitchenQueueSummary, Order } from "@/types/order";
import { getKitchenOrders, getKitchenQueue } from "@/lib/api/kitchenApi";
import { useOrderHub } from "@/lib/realtime/orderHub";

export interface UseKitchenBoardResult {
  newOrders: Order[];
  inProgressOrders: Order[];
  queue: KitchenQueueSummary;
  isEmpty: boolean;
  isLoading: boolean;
  isError: boolean;
}

const EMPTY_QUEUE: KitchenQueueSummary = {
  pending: 0,
  inProgress: 0,
  ready: 0,
};

export function useKitchenBoard(): UseKitchenBoardResult {
  const { tenantId } = useTenant();
  useOrderHub();

  const {
    data: orders = [],
    isLoading: ordersLoading,
    isError: ordersError,
  } = useQuery({
    queryKey: queryKeys.orders.kitchen(tenantId),
    queryFn: getKitchenOrders,
  });

  const { data: queue = EMPTY_QUEUE, isError: queueError } = useQuery({
    queryKey: queryKeys.orders.kitchenQueue(tenantId),
    queryFn: getKitchenQueue,
  });

  const newOrders = orders
    .filter((o) => o.status === OrderStatus.New)
    .sort(
      (a, b) =>
        new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
    );

  const inProgressOrders = orders
    .filter((o) => o.status === OrderStatus.InProgress)
    .sort(
      (a, b) =>
        new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
    );

  const activeOrders = newOrders.length + inProgressOrders.length;

  return {
    newOrders,
    inProgressOrders,
    queue,
    isEmpty: activeOrders === 0,
    isLoading: ordersLoading,
    isError: ordersError || queueError,
  };
}
