import apiClient from "@/lib/api/apiClient";
import type { Restaurant, RestaurantPlan, RestaurantStatus } from "@/types";
import type { RestaurantFormValues } from "@/lib/validations";

interface PagedResponse<T> {
  data: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function getRestaurants(): Promise<Restaurant[]> {
  const res = await apiClient.get<PagedResponse<Restaurant>>("/v1/admin/restaurants");
  return res.data.data;
}

export async function getRestaurant(id: number): Promise<Restaurant> {
  const res = await apiClient.get<{ data: Restaurant }>(`/v1/admin/restaurants/${id}`);
  return res.data.data;
}

export async function createRestaurant(data: RestaurantFormValues): Promise<Restaurant> {
  const res = await apiClient.post<{ data: Restaurant }>("/v1/admin/restaurants", data);
  return res.data.data;
}

export async function updateRestaurantStatus(
  id: number,
  status: RestaurantStatus
): Promise<Restaurant> {
  const res = await apiClient.patch<{ data: Restaurant }>(
    `/v1/admin/restaurants/${id}/status`,
    { status }
  );
  return res.data.data;
}

export async function updateRestaurantPlan(
  id: number,
  plan: RestaurantPlan
): Promise<Restaurant> {
  const res = await apiClient.patch<{ data: Restaurant }>(
    `/v1/admin/restaurants/${id}/plan`,
    { plan }
  );
  return res.data.data;
}
