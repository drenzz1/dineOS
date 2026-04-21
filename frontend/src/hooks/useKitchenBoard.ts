// TODO: replace with real API call when backend is ready
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

// ─── Mock data ────────────────────────────────────────────────────────────────

function ago(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

const MOCK_KITCHEN_ORDERS: Order[] = [
  {
    id: "ktc-001",
    orderType: "dine-in",
    tableNumber: 3,
    status: OrderStatus.New,
    items: [
      { id: "k1i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "k1i2", name: "Caesar Salad", quantity: 2, unitPrice: 8.99 },
      { id: "k1i3", name: "Sparkling Water", quantity: 2, unitPrice: 1.99 },
    ],
    createdAt: ago(3),
    updatedAt: ago(3),
  },
  {
    id: "ktc-002",
    orderType: "pickup",
    status: OrderStatus.New,
    items: [
      { id: "k2i1", name: "Pepperoni Pizza", quantity: 2, unitPrice: 14.99 },
      { id: "k2i2", name: "Coca Cola", quantity: 2, unitPrice: 2.99 },
    ],
    createdAt: ago(7),
    updatedAt: ago(7),
  },
  {
    id: "ktc-003",
    orderType: "dine-in",
    tableNumber: 1,
    status: OrderStatus.InProgress,
    items: [
      { id: "k3i1", name: "Pasta Carbonara", quantity: 1, unitPrice: 13.99 },
      { id: "k3i2", name: "Tiramisu", quantity: 1, unitPrice: 5.99 },
    ],
    notes: "Nut allergy — no pesto",
    createdAt: ago(15),
    updatedAt: ago(12),
  },
  {
    id: "ktc-004",
    orderType: "dine-in",
    tableNumber: 6,
    status: OrderStatus.InProgress,
    items: [
      { id: "k4i1", name: "Margherita Pizza", quantity: 2, unitPrice: 12.99 },
      { id: "k4i2", name: "Pepperoni Pizza", quantity: 1, unitPrice: 14.99 },
      { id: "k4i3", name: "Sparkling Water", quantity: 3, unitPrice: 1.99 },
    ],
    notes: "Extra crispy base on all pizzas",
    createdAt: ago(8),
    updatedAt: ago(6),
  },
];

// ─── Fetch function ───────────────────────────────────────────────────────────

async function fetchKitchenOrders(): Promise<Order[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return MOCK_KITCHEN_ORDERS.filter(
    (o) =>
      o.status === OrderStatus.New || o.status === OrderStatus.InProgress
  );
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export interface UseKitchenBoardResult {
  newOrders: Order[];
  inProgressOrders: Order[];
  isEmpty: boolean;
  isLoading: boolean;
  isError: boolean;
}

export function useKitchenBoard(): UseKitchenBoardResult {
  const { data: orders = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.orders.kitchen(),
    queryFn: fetchKitchenOrders,
    // TODO: replace polling with SignalR when backend is ready
    refetchInterval: 10_000,
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

  return {
    newOrders,
    inProgressOrders,
    isEmpty: orders.length === 0,
    isLoading,
    isError,
  };
}
