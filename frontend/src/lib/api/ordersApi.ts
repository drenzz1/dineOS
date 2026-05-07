// TODO: wire to real backend orders API — see GitHub issue #104
// TODO: wire to real backend payments API — see GitHub issue #103
import { OrderStatus, MenuCategory } from "@/types";
import type { MenuItem, Order } from "@/types";
import type { OrderFormValues } from "@/lib/validations/order";

const MOCK_MENU_ITEMS: MenuItem[] = [
  { id: "1", name: "Margherita Pizza", price: 12.99, category: MenuCategory.MainCourse },
  { id: "2", name: "Pepperoni Pizza", price: 14.99, category: MenuCategory.MainCourse },
  { id: "3", name: "Caesar Salad", price: 8.99, category: MenuCategory.Starters },
  { id: "4", name: "Pasta Carbonara", price: 13.99, category: MenuCategory.MainCourse },
  { id: "5", name: "Coca Cola", price: 2.99, category: MenuCategory.Drinks },
  { id: "6", name: "Sparkling Water", price: 1.99, category: MenuCategory.Drinks },
  { id: "7", name: "Tiramisu", price: 5.99, category: MenuCategory.Desserts },
];

function ago(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

// 5 seed orders covering all statuses and timing thresholds for the live board.
// InProgress ord-003 is 25 min old → red border; ord-002 is 12 min old → amber border.
let mockOrders: Order[] = [
  {
    id: "ord-001",
    orderType: "dine-in",
    tableNumber: 3,
    status: OrderStatus.New,
    items: [
      { id: "i1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "i2", name: "Caesar Salad", quantity: 2, unitPrice: 8.99 },
    ],
    notes: "Extra cheese on the pizza please",
    createdAt: ago(2),
    updatedAt: ago(2),
  },
  {
    id: "ord-002",
    orderType: "pickup",
    status: OrderStatus.InProgress,
    items: [
      { id: "i3", name: "Pasta Carbonara", quantity: 1, unitPrice: 13.99 },
      { id: "i4", name: "Coca Cola", quantity: 2, unitPrice: 2.99 },
    ],
    createdAt: ago(15),
    updatedAt: ago(12), // 12 min in InProgress → amber border
  },
  {
    id: "ord-003",
    orderType: "dine-in",
    tableNumber: 7,
    status: OrderStatus.InProgress,
    items: [
      { id: "i5", name: "Pepperoni Pizza", quantity: 2, unitPrice: 14.99 },
      { id: "i6", name: "Sparkling Water", quantity: 2, unitPrice: 1.99 },
    ],
    notes: "Nut allergy — no pesto",
    createdAt: ago(40),
    updatedAt: ago(25), // 25 min in InProgress → red border
  },
  {
    id: "ord-004",
    orderType: "dine-in",
    tableNumber: 1,
    status: OrderStatus.Ready,
    items: [
      { id: "i7", name: "Tiramisu", quantity: 3, unitPrice: 5.99 },
      { id: "i8", name: "Sparkling Water", quantity: 3, unitPrice: 1.99 },
    ],
    createdAt: ago(30),
    updatedAt: ago(5),
  },
  {
    id: "ord-005",
    orderType: "pickup",
    status: OrderStatus.Delivered,
    items: [
      { id: "i9", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 },
      { id: "i10", name: "Tiramisu", quantity: 1, unitPrice: 5.99 },
    ],
    createdAt: ago(60),
    updatedAt: ago(15),
  },
];

export async function getMenuItems(): Promise<MenuItem[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return MOCK_MENU_ITEMS;
}

export async function getOrders(): Promise<Order[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockOrders;
}

export async function createOrder(data: OrderFormValues): Promise<Order> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  const order: Order = {
    id: crypto.randomUUID(),
    orderType: data.orderType,
    tableNumber: data.tableNumber,
    status: OrderStatus.New,
    items: data.items.map((item) => ({
      id: crypto.randomUUID(),
      name: item.name,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
    })),
    notes: data.notes,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
  mockOrders = [...mockOrders, order];
  return order;
}

export async function updateOrderStatus(
  orderId: string,
  status: OrderStatus
): Promise<Order> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  const order = mockOrders.find((o) => o.id === orderId);
  if (!order) throw new Error(`Order ${orderId} not found`);
  const updated: Order = {
    ...order,
    status,
    updatedAt: new Date().toISOString(),
  };
  mockOrders = mockOrders.map((o) => (o.id === orderId ? updated : o));
  return updated;
}
