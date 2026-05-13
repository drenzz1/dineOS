import apiClient from "@/lib/api/apiClient";
import type { MeResponse } from "@/types/me";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export async function getMe(): Promise<MeResponse> {
  try {
    const res = await apiClient.get<ApiResponse<MeResponse>>("/v1/me");
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
