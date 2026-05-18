import apiClient from "@/lib/api/apiClient";
import { mapOrder, type OrderDto } from "@/lib/api/ordersApi";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import { OrderStatus } from "@/types/order";
import type { KitchenQueueSummary, Order, OrderItem } from "@/types/order";

function isDevAuth(): boolean {
  if (typeof document === "undefined") return false;
  return document.cookie.split("; ").includes("access_token=dev");
}

interface KitchenQueueSummaryDto {
  pending: number;
  inProgress: number;
  ready: number;
}

function ago(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

function itemsTotal(items: OrderItem[]): number {
  return items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0);
}

const DEV_KITCHEN_ORDERS_RAW: Order[] = [
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
    total: 0,
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
    total: 0,
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
    total: 0,
    createdAt: ago(15),
    updatedAt: ago(12),
  },
];

const DEV_KITCHEN_ORDERS: Order[] = DEV_KITCHEN_ORDERS_RAW.map((o) => ({
  ...o,
  total: itemsTotal(o.items),
}));

export async function getKitchenOrders(): Promise<Order[]> {
  if (isDevAuth()) {
    await new Promise<void>((resolve) => setTimeout(resolve, 200));
    return DEV_KITCHEN_ORDERS;
  }

  try {
    const res = await apiClient.get<ApiResponse<OrderDto[]>>(
      "/v1/kitchen/orders"
    );
    return unwrap(res).map(mapOrder);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getKitchenQueue(): Promise<KitchenQueueSummary> {
  if (isDevAuth()) {
    const pending = DEV_KITCHEN_ORDERS.filter(
      (o) => o.status === OrderStatus.New
    ).length;
    const inProgress = DEV_KITCHEN_ORDERS.filter(
      (o) => o.status === OrderStatus.InProgress
    ).length;
    const ready = DEV_KITCHEN_ORDERS.filter(
      (o) => o.status === OrderStatus.Ready
    ).length;
    return { pending, inProgress, ready };
  }

  try {
    const res = await apiClient.get<ApiResponse<KitchenQueueSummaryDto>>(
      "/v1/kitchen/queue"
    );
    const dto = unwrap(res);
    return {
      pending: dto.pending,
      inProgress: dto.inProgress,
      ready: dto.ready,
    };
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateKitchenOrderStatus(
  orderId: string,
  status: OrderStatus
): Promise<Order> {
  try {
    const res = await apiClient.put<ApiResponse<OrderDto>>(
      `/v1/kitchen/orders/${orderId}/status`,
      { status }
    );
    return mapOrder(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}
