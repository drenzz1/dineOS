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
  status: OrderStatus;
  items: OrderItem[];
  createdAt: string;
  updatedAt: string;
}
