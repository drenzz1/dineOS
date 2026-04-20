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

let mockOrders: Order[] = [];

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
