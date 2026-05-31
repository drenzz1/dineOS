import axios from "axios";
import { getBusinessToken } from "@/lib/auth/keycloak";
import type { StaffSession } from "@/types";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

// Dedicated client WITHOUT the apiClient interceptor: starting a staff session
// must authenticate with the business (Keycloak) token, not whatever is in the
// active `access_token` cookie (which becomes the staff-session token once a
// PIN is entered). POST /auth/staff-session only accepts the Keycloak scheme.
const businessClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? "/api",
});

export async function startStaffSession(
  staffMemberId: number,
  pin: string
): Promise<StaffSession> {
  try {
    const businessToken = getBusinessToken();
    const res = await businessClient.post<ApiResponse<StaffSession>>(
      "/v1/auth/staff-session",
      { staffMemberId, pin },
      businessToken
        ? { headers: { Authorization: `Bearer ${businessToken}` } }
        : undefined
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

// Server-side revocation of a staff session (ending a shift / switching user).
// Authenticated by the staff access token; the refresh token is revoked too.
// Best-effort: callers proceed with local cleanup regardless of the result.
export async function endStaffSession(
  accessToken: string,
  refreshToken: string
): Promise<void> {
  await businessClient.post(
    "/v1/auth/staff-session/end",
    { refreshToken },
    { headers: { Authorization: `Bearer ${accessToken}` } }
  );
}
