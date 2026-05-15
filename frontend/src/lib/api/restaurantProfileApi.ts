import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import type { RestaurantProfile } from "@/types/restaurantProfile";
import type { RestaurantProfileFormValues } from "@/lib/validations/restaurantProfile";

export async function getRestaurantProfile(): Promise<RestaurantProfile> {
  try {
    const res = await apiClient.get<ApiResponse<RestaurantProfile>>("/v1/restaurant");
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function updateRestaurantProfile(
  data: RestaurantProfileFormValues
): Promise<RestaurantProfile> {
  try {
    const res = await apiClient.put<ApiResponse<RestaurantProfile>>(
      "/v1/restaurant",
      data
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
