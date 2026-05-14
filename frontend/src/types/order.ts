export type OrderType = "dine-in" | "pickup";

export enum OrderStatus {
  New = "New",
  InProgress = "InProgress",
  Ready = "Ready",
  Delivered = "Delivered",
  Cancelled = "Cancelled",
}

export interface OrderItem {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: string;
  tenantId?: string;
  orderType: OrderType;
  tableNumber?: number;
  status: OrderStatus;
  items: OrderItem[];
  total: number;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}
