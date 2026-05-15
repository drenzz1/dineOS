import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import type { RestaurantTable } from "@/types/restaurantTable";
import type {
  CreateRestaurantTableFormValues,
  UpdateRestaurantTableFormValues,
} from "@/lib/validations/restaurantTable";

export async function listRestaurantTables(): Promise<RestaurantTable[]> {
  try {
    const res = await apiClient.get<ApiResponse<RestaurantTable[]>>(
      "/v1/restaurant/tables"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createRestaurantTable(
  data: CreateRestaurantTableFormValues
): Promise<RestaurantTable> {
  try {
    const res = await apiClient.post<ApiResponse<RestaurantTable>>(
      "/v1/restaurant/tables",
      data
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateRestaurantTable(
  id: number,
  data: UpdateRestaurantTableFormValues
): Promise<RestaurantTable> {
  try {
    const res = await apiClient.put<ApiResponse<RestaurantTable>>(
      `/v1/restaurant/tables/${id}`,
      data
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
