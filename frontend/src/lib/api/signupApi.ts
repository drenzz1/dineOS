import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export interface SignupPayload {
  restaurantName: string;
  ownerName: string;
  ownerEmail: string;
  phone: string;
  city: string;
}

export interface SignupResult {
  checkoutUrl: string;
  sessionId: string;
  tenantId: number;
}

export interface SignupStatus {
  status: "PendingPayment" | "Active" | "Failed";
}

export async function startSignup(payload: SignupPayload): Promise<SignupResult> {
  try {
    const res = await apiClient.post<ApiResponse<SignupResult>>("/v1/signup", payload);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getSignupStatus(sessionId: string): Promise<SignupStatus> {
  try {
    const res = await apiClient.get<ApiResponse<SignupStatus>>("/v1/signup/status", {
      params: { sessionId },
    });
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export interface SetPasswordPayload {
  token: string;
  newPassword: string;
}

export async function setPassword(payload: SetPasswordPayload): Promise<string> {
  try {
    const res = await apiClient.post<ApiResponse<string>>("/v1/signup/set-password", payload);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
