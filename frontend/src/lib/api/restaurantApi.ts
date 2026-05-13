import apiClient from "@/lib/api/apiClient";
import type { Restaurant, RestaurantPlan, RestaurantStatus } from "@/types";
import type { RestaurantFormValues } from "@/lib/validations";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

interface PagedRestaurantData {
  items: Restaurant[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function getRestaurants(): Promise<Restaurant[]> {
  try {
    const res = await apiClient.get<ApiResponse<PagedRestaurantData>>(
      "/v1/admin/restaurants"
    );
    return unwrap(res).items;
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getRestaurant(id: number): Promise<Restaurant> {
  try {
    const res = await apiClient.get<ApiResponse<Restaurant>>(`/v1/admin/restaurants/${id}`);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createRestaurant(data: RestaurantFormValues): Promise<Restaurant> {
  try {
    const res = await apiClient.post<ApiResponse<Restaurant>>("/v1/admin/restaurants", data);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateRestaurantStatus(
  id: number,
  status: RestaurantStatus
): Promise<Restaurant> {
  try {
    const res = await apiClient.patch<ApiResponse<Restaurant>>(
      `/v1/admin/restaurants/${id}/status`,
      { status }
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateRestaurantPlan(
  id: number,
  plan: RestaurantPlan
): Promise<Restaurant> {
  try {
    const res = await apiClient.patch<ApiResponse<Restaurant>>(
      `/v1/admin/restaurants/${id}/plan`,
      { plan }
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function deleteRestaurant(id: number): Promise<void> {
  try {
    await apiClient.delete(`/v1/admin/restaurants/${id}`);
  } catch (error) {
    throw toApiError(error);
  }
}
