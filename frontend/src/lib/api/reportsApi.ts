import apiClient from "@/lib/api/apiClient";
import type { SalesReport, OrdersReport, StaffReport, ItemsReport, OrderHistoryReport } from "@/types/reports";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export async function getSalesReport(from: string, to: string): Promise<SalesReport> {
  try {
    const res = await apiClient.get<ApiResponse<SalesReport>>("/v1/reports/sales", {
      params: { from, to },
    });
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getOrdersReport(from: string, to: string): Promise<OrdersReport> {
  try {
    const res = await apiClient.get<ApiResponse<OrdersReport>>("/v1/reports/orders", {
      params: { from, to },
    });
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getStaffReport(): Promise<StaffReport> {
  try {
    const res = await apiClient.get<ApiResponse<StaffReport>>("/v1/reports/staff");
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getItemsReport(from: string, to: string): Promise<ItemsReport> {
  try {
    const res = await apiClient.get<ApiResponse<ItemsReport>>("/v1/reports/items", {
      params: { from, to },
    });
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getOrderHistory(
  from: string,
  to: string,
  page: number,
  pageSize = 25,
): Promise<OrderHistoryReport> {
  try {
    const res = await apiClient.get<ApiResponse<OrderHistoryReport>>("/v1/reports/orders/history", {
      params: { from, to, page, pageSize },
    });
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
