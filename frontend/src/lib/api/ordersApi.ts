import apiClient from "@/lib/api/apiClient";
import type { OrderFormValues } from "@/lib/validations/order";
import type { MenuItem, Order } from "@/types";
import { OrderStatus } from "@/types";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export interface OrderDto {
  id: number;
  tenantId?: number;
  orderType: "dine-in" | "pickup";
  tableNumber?: number | null;
  status: OrderStatus;
  total: number;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
  items: OrderItemDto[];
}

interface OrderItemDto {
  id: number;
  name: string;
  quantity: number;
  unitPrice: number;
  notes?: string | null;
}

interface MenuItemDto {
  id: number;
  tenantId?: number;
  name: string;
  price: number;
  category: string;
  description?: string | null;
  imageUrl?: string | null;
}

export interface GetOrdersParams {
  date?: string;
  status?: OrderStatus | "all";
}

export function mapOrder(dto: OrderDto): Order {
  return {
    id: String(dto.id),
    tenantId: dto.tenantId == null ? undefined : String(dto.tenantId),
    orderType: dto.orderType,
    tableNumber: dto.tableNumber ?? undefined,
    status: dto.status,
    items: dto.items.map((item) => ({
      id: String(item.id),
      name: item.name,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
    })),
    total: dto.total,
    notes: dto.notes ?? undefined,
    createdAt: dto.createdAt,
    updatedAt: dto.updatedAt,
  };
}

function mapMenuItem(dto: MenuItemDto): MenuItem {
  return {
    id: String(dto.id),
    tenantId: dto.tenantId == null ? undefined : String(dto.tenantId),
    name: dto.name,
    price: dto.price,
    category: dto.category,
    description: dto.description ?? undefined,
    imageUrl: dto.imageUrl ?? undefined,
  };
}

function buildCreateOrderRequest(data: OrderFormValues) {
  return {
    orderType: data.orderType,
    tableNumber: data.orderType === "dine-in" ? data.tableNumber : null,
    notes: data.notes?.trim() ? data.notes.trim() : null,
    items: data.items.map((item) => ({
      name: item.name,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
    })),
  };
}

export async function getMenuItems(): Promise<MenuItem[]> {
  try {
    const res = await apiClient.get<ApiResponse<MenuItemDto[]>>("/v1/menu/items");
    return unwrap(res).map(mapMenuItem);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getOrders(params: GetOrdersParams = {}): Promise<Order[]> {
  try {
    const res = await apiClient.get<ApiResponse<OrderDto[]>>("/v1/orders", {
      params: {
        date: params.date,
        status: params.status && params.status !== "all" ? params.status : undefined,
      },
    });
    return unwrap(res).map(mapOrder);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getOrder(orderId: string): Promise<Order> {
  try {
    const res = await apiClient.get<ApiResponse<OrderDto>>(`/v1/orders/${orderId}`);
    return mapOrder(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createOrder(data: OrderFormValues): Promise<Order> {
  try {
    const res = await apiClient.post<ApiResponse<OrderDto>>(
      "/v1/orders",
      buildCreateOrderRequest(data)
    );
    return mapOrder(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateOrderStatus(
  orderId: string,
  status: OrderStatus
): Promise<Order> {
  try {
    const res = await apiClient.patch<ApiResponse<OrderDto>>(
      `/v1/orders/${orderId}/status`,
      { status }
    );
    return mapOrder(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}
