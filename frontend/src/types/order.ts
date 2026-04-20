export type OrderType = "dine-in" | "pickup";

export enum OrderStatus {
  New = "New",
  InProgress = "InProgress",
  Ready = "Ready",
  Delivered = "Delivered",
  Cancelled = "Cancelled",
}

export interface MenuItem {
  id: string;
  name: string;
  price: number;
  category: string;
}

export interface OrderItem {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: string;
  orderType: OrderType;
  tableNumber?: number;
  status: OrderStatus;
  items: OrderItem[];
  notes?: string;
  createdAt: string;
  updatedAt: string;
}
