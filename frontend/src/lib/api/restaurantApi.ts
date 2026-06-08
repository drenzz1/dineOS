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

export interface GetRestaurantsParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

// The backend filters and pages server-side; without params it returns only the
// first page (default 20), which would silently truncate the admin list. We pass
// the search term through and request the max page size so the table reflects all
// matches rather than filtering an already-truncated slice on the client.
export async function getRestaurants(
  params: GetRestaurantsParams = {}
): Promise<Restaurant[]> {
  try {
    const res = await apiClient.get<ApiResponse<PagedRestaurantData>>(
      "/v1/admin/restaurants",
      { params }
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

interface ResendVerificationEmailResponse {
  jobId: string;
}

export async function resendEmailVerification(
  tenantId: number
): Promise<{ jobId: string }> {
  try {
    const res = await apiClient.post<ApiResponse<ResendVerificationEmailResponse>>(
      `/v1/admin/restaurants/${tenantId}/email-verification/resend`
    );
    return { jobId: unwrap(res).jobId };
  } catch (error) {
    throw toApiError(error);
  }
}

export async function confirmEmailVerification(tenantId: number, code: string): Promise<void> {
  try {
    await apiClient.post<ApiResponse<boolean>>(
      `/v1/admin/restaurants/${tenantId}/email-verification/confirm`,
      { code }
    );
  } catch (error) {
    throw toApiError(error);
  }
}
