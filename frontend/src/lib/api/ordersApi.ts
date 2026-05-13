import axios from "axios";
import apiClient from "@/lib/api/apiClient";
import type { OrderFormValues } from "@/lib/validations/order";
import type { MenuItem, Order } from "@/types";
import { OrderStatus } from "@/types";

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: string[] | null;
}

interface OrderDto {
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

function unwrap<T>(body: ApiResponse<T>, fallback: string): T {
  if (!body.success || body.data == null) {
    throw new Error(body.errors?.[0] ?? body.message ?? fallback);
  }

  return body.data;
}

function toApiError(error: unknown, fallback: string): Error {
  if (axios.isAxiosError(error)) {
    const body = error.response?.data as ApiResponse<unknown> | undefined;
    return new Error(body?.errors?.[0] ?? body?.message ?? fallback);
  }

  return error instanceof Error ? error : new Error(fallback);
}

function mapOrder(dto: OrderDto): Order {
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
    return unwrap(res.data, "Failed to load menu items.").map(mapMenuItem);
  } catch (error) {
    throw toApiError(error, "Failed to load menu items.");
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
    return unwrap(res.data, "Failed to load orders.").map(mapOrder);
  } catch (error) {
    throw toApiError(error, "Failed to load orders.");
  }
}

export async function getOrder(orderId: string): Promise<Order> {
  try {
    const res = await apiClient.get<ApiResponse<OrderDto>>(`/v1/orders/${orderId}`);
    return mapOrder(unwrap(res.data, `Order ${orderId} not found.`));
  } catch (error) {
    throw toApiError(error, `Order ${orderId} not found.`);
  }
}

export async function createOrder(data: OrderFormValues): Promise<Order> {
  try {
    const res = await apiClient.post<ApiResponse<OrderDto>>(
      "/v1/orders",
      buildCreateOrderRequest(data)
    );
    return mapOrder(unwrap(res.data, "Failed to create order."));
  } catch (error) {
    throw toApiError(error, "Failed to create order.");
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
    return mapOrder(unwrap(res.data, `Failed to update order ${orderId}.`));
  } catch (error) {
    throw toApiError(error, `Failed to update order ${orderId}.`);
  }
}
