// TODO: replace with real API call when backend is ready
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { OrderStatus } from "@/types/order";
import type { Order, OrderItem } from "@/types/order";

// ─── Types ────────────────────────────────────────────────────────────────────

export interface DailySummary {
  totalOrders: number;
  totalRevenue: number;
  cancelledOrders: number;
  avgPrepTimeMinutes: number;
}

// ─── Mock data ────────────────────────────────────────────────────────────────

function atTime(hour: number, minute: number): string {
  const d = new Date();
  d.setHours(hour, minute, 0, 0);
  return d.toISOString();
}

const MOCK_ORDERS_TODAY: Omit<Order, "total">[] = [
  {
    id: "day-001",
    orderType: "dine-in",
    tableNumber: 2,
    status: OrderStatus.Delivered,
    items: [
      { id: "d1i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "d1i2", name: "Caesar Salad", quantity: 1, unitPrice: 8.99 },
      { id: "d1i3", name: "Coca Cola", quantity: 2, unitPrice: 2.99 },
    ],
    createdAt: atTime(9, 15),
    updatedAt: atTime(9, 42), // 27 min prep
  },
  {
    id: "day-002",
    orderType: "pickup",
    status: OrderStatus.Delivered,
    items: [
      { id: "d2i1", name: "Pasta Carbonara", quantity: 1, unitPrice: 13.99 },
      { id: "d2i2", name: "Coca Cola", quantity: 2, unitPrice: 2.99 },
    ],
    createdAt: atTime(9, 30),
    updatedAt: atTime(9, 48), // 18 min prep
  },
  {
    id: "day-003",
    orderType: "dine-in",
    tableNumber: 5,
    status: OrderStatus.Delivered,
    items: [
      { id: "d3i1", name: "Pepperoni Pizza", quantity: 2, unitPrice: 14.99 },
      { id: "d3i2", name: "Sparkling Water", quantity: 2, unitPrice: 1.99 },
    ],
    createdAt: atTime(10, 5),
    updatedAt: atTime(10, 28), // 23 min prep
  },
  {
    id: "day-004",
    orderType: "pickup",
    status: OrderStatus.Delivered,
    items: [
      { id: "d4i1", name: "Tiramisu", quantity: 2, unitPrice: 5.99 },
      { id: "d4i2", name: "Sparkling Water", quantity: 2, unitPrice: 1.99 },
    ],
    createdAt: atTime(11, 20),
    updatedAt: atTime(11, 45), // 25 min prep
  },
  {
    id: "day-005",
    orderType: "dine-in",
    tableNumber: 1,
    status: OrderStatus.InProgress,
    items: [
      { id: "d5i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "d5i2", name: "Pasta Carbonara", quantity: 1, unitPrice: 13.99 },
    ],
    createdAt: atTime(12, 10),
    updatedAt: atTime(12, 10),
  },
  {
    id: "day-006",
    orderType: "pickup",
    status: OrderStatus.InProgress,
    items: [
      { id: "d6i1", name: "Caesar Salad", quantity: 2, unitPrice: 8.99 },
    ],
    createdAt: atTime(12, 25),
    updatedAt: atTime(12, 25),
  },
  {
    id: "day-007",
    orderType: "dine-in",
    tableNumber: 4,
    status: OrderStatus.Ready,
    items: [
      { id: "d7i1", name: "Pepperoni Pizza", quantity: 1, unitPrice: 14.99 },
      { id: "d7i2", name: "Tiramisu", quantity: 1, unitPrice: 5.99 },
      { id: "d7i3", name: "Coca Cola", quantity: 1, unitPrice: 2.99 },
    ],
    createdAt: atTime(12, 30),
    updatedAt: atTime(12, 52),
  },
  {
    id: "day-008",
    orderType: "pickup",
    status: OrderStatus.New,
    items: [
      { id: "d8i1", name: "Pasta Carbonara", quantity: 1, unitPrice: 13.99 },
    ],
    createdAt: atTime(13, 0),
    updatedAt: atTime(13, 0),
  },
  {
    id: "day-009",
    orderType: "dine-in",
    tableNumber: 3,
    status: OrderStatus.Cancelled,
    items: [
      { id: "d9i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "d9i2", name: "Caesar Salad", quantity: 1, unitPrice: 8.99 },
    ],
    notes: "Customer left before food was ready",
    createdAt: atTime(11, 45),
    updatedAt: atTime(12, 5),
  },
  {
    id: "day-010",
    orderType: "dine-in",
    tableNumber: 6,
    status: OrderStatus.New,
    items: [
      { id: "d10i1", name: "Pepperoni Pizza", quantity: 2, unitPrice: 14.99 },
      { id: "d10i2", name: "Sparkling Water", quantity: 2, unitPrice: 1.99 },
      { id: "d10i3", name: "Tiramisu", quantity: 2, unitPrice: 5.99 },
    ],
    createdAt: atTime(13, 10),
    updatedAt: atTime(13, 10),
  },
];

// ─── Helpers ──────────────────────────────────────────────────────────────────

function itemsTotal(items: OrderItem[]): number {
  return items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0);
}

function computeSummary(orders: Order[]): DailySummary {
  const delivered = orders.filter((o) => o.status === OrderStatus.Delivered);

  const totalRevenue = delivered.reduce(
    (sum, o) => sum + itemsTotal(o.items),
    0
  );

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

async function fetchOrdersForDate(date: string): Promise<Order[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  const today = new Date().toISOString().split("T")[0];
  const source = date === today ? MOCK_ORDERS_TODAY : [];
  return source.map((o) => ({ ...o, total: itemsTotal(o.items) }));
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
    queryFn: () => fetchOrdersForDate(date),
  });

  return { orders, summary: computeSummary(orders), isLoading, isError };
}
