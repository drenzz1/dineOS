export type PaymentMethod = "Cash" | "Card";

export type PaymentStatus = "Completed" | "Refunded" | "Pending";

export interface Payment {
  id: string;
  orderId: string;
  amount: number;
  method: PaymentMethod;
  status: PaymentStatus;
  tenantId?: string;
  createdAt: string;
}

export interface ProcessPaymentInput {
  orderId: string;
  amount: number;
  method: PaymentMethod;
}
