import apiClient from "@/lib/api/apiClient";
import { mapOrder, type OrderDto } from "@/lib/api/ordersApi";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import type { Order } from "@/types/order";
import type {
  Payment,
  PaymentMethod,
  PaymentStatus,
  ProcessPaymentInput,
} from "@/types/payment";

interface PaymentDto {
  id: number;
  orderId: number;
  amount: number;
  method: string;
  status: string;
  tenantId?: number;
  createdAt: string;
}

function mapPayment(dto: PaymentDto): Payment {
  return {
    id: String(dto.id),
    orderId: String(dto.orderId),
    amount: dto.amount,
    method: dto.method as PaymentMethod,
    status: dto.status as PaymentStatus,
    tenantId: dto.tenantId == null ? undefined : String(dto.tenantId),
    createdAt: dto.createdAt,
  };
}

export async function getOpenOrders(): Promise<Order[]> {
  try {
    const res = await apiClient.get<ApiResponse<OrderDto[]>>(
      "/v1/payments/open-orders"
    );
    return unwrap(res).map(mapOrder);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function processPayment(
  input: ProcessPaymentInput
): Promise<Payment> {
  try {
    const res = await apiClient.post<ApiResponse<PaymentDto>>("/v1/payments", {
      orderId: Number(input.orderId),
      amount: input.amount,
      method: input.method,
    });
    return mapPayment(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}
