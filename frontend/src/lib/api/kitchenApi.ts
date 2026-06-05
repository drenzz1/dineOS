import apiClient from "@/lib/api/apiClient";
import { mapOrder, type OrderDto } from "@/lib/api/ordersApi";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import { OrderStatus } from "@/types/order";
import type { KitchenQueueSummary, Order } from "@/types/order";

interface KitchenQueueSummaryDto {
  pending: number;
  inProgress: number;
  ready: number;
}

export async function getKitchenOrders(): Promise<Order[]> {
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
