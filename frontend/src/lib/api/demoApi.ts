import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export interface DemoAccessPayload {
  email: string;
  acceptedTerms: boolean;
  companyName?: string;
}

export interface DemoAccessResult {
  message: string;
}

export async function requestDemoAccess(
  payload: DemoAccessPayload
): Promise<DemoAccessResult> {
  try {
    const res = await apiClient.post<ApiResponse<DemoAccessResult>>(
      "/v1/demo/request",
      payload
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
