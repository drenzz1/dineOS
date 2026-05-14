import apiClient from "@/lib/api/apiClient";
import type { OrderFormValues } from "@/lib/validations/order";
import type { MenuItem, Order } from "@/types";
import { MenuCategory, OrderStatus } from "@/types";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

function isDevAuth(): boolean {
  if (typeof document === "undefined") return false;
  return document.cookie.split("; ").includes("access_token=dev");
}

const MOCK_MENU_ITEMS: MenuItem[] = [
  { id: "1", name: "Margherita Pizza", price: 12.99, category: MenuCategory.MainCourse },
  { id: "2", name: "Pepperoni Pizza", price: 14.99, category: MenuCategory.MainCourse },
  { id: "3", name: "Caesar Salad", price: 9.99, category: MenuCategory.Starters },
  { id: "4", name: "Tiramisu", price: 7.99, category: MenuCategory.Desserts },
  { id: "5", name: "Espresso", price: 3.99, category: MenuCategory.Drinks },
  { id: "6", name: "Garlic Bread", price: 5.99, category: MenuCategory.Sides },
  { id: "7", name: "Mineral Water", price: 2.99, category: MenuCategory.Drinks },
];

let MOCK_ORDERS: Order[] = [
  {
    id: "ord-001",
    orderType: "dine-in",
    tableNumber: 3,
    status: OrderStatus.New,
    items: [{ id: "oi-1", name: "Margherita Pizza", quantity: 1, unitPrice: 12.99 }],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

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
  if (isDevAuth()) {
    return MOCK_MENU_ITEMS;
  }

  try {
    const res = await apiClient.get<ApiResponse<MenuItemDto[]>>("/v1/menu/items");
    return unwrap(res).map(mapMenuItem);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getOrders(params: GetOrdersParams = {}): Promise<Order[]> {
  if (isDevAuth()) {
    return MOCK_ORDERS;
  }

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
  if (isDevAuth()) {
    const subtotal = data.items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
    const order: Order = {
      id: `dev-${Date.now()}`,
      orderType: data.orderType,
      tableNumber: data.tableNumber ?? undefined,
      status: OrderStatus.New,
      items: data.items.map((item, idx) => ({
        id: `dev-oi-${idx}`,
        name: item.name,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
      notes: data.notes?.trim() || undefined,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    MOCK_ORDERS = [...MOCK_ORDERS, order];
    return order;
  }

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
