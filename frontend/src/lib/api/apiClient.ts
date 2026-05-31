import axios, { AxiosError, InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "@/stores/authStore";
import {
  persistAuthCookies,
  persistStaffSessionCookies,
  getStaffRefreshToken,
  clearAuthCookies,
} from "@/lib/auth/keycloak";
import type { AppRole } from "@/types";

const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? "/api",
});

const refreshClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? "/api",
});

interface RefreshResponseData {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  refreshExpiresIn: number | null;
}

interface RefreshApiResponse {
  success: boolean;
  data: RefreshResponseData | null;
  message: string;
  errors: string[] | null;
}

interface StaffRefreshApiResponse {
  success: boolean;
  data: { accessToken: string; role: AppRole; expiresIn: number } | null;
  message: string;
  errors: string[] | null;
}

interface RetryConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

function getCookie(name: string): string | null {
  if (typeof document === "undefined") {
    return null;
  }

  const cookie = document.cookie
    .split("; ")
    .find((row) => row.startsWith(`${name}=`));

  return cookie ? decodeURIComponent(cookie.split("=")[1] ?? "") : null;
}

apiClient.interceptors.request.use((config) => {
  const tenantId = useAuthStore.getState().tenantId;
  if (tenantId !== null) {
    config.headers["X-Tenant-ID"] = tenantId;
  }
  return config;
});

apiClient.interceptors.request.use((config) => {
  const token = getCookie("access_token");
  if (token) {
    config.headers["Authorization"] = `Bearer ${token}`;
  }
  return config;
});

let refreshPromise: Promise<void> | null = null;
let staffRefreshPromise: Promise<void> | null = null;

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryConfig | undefined;

    if (error.response?.status !== 401 || originalRequest?._retry) {
      return Promise.reject(error);
    }

    // A staff-session token is refreshed via the STAFF refresh endpoint, never
    // the Keycloak refresh token (which would mint an owner token and silently
    // escalate a Cashier session). On success the new access token is swapped in
    // and the request retried; on failure (expired/revoked) we restore owner
    // mode and bounce to /select-staff to re-PIN — never retrying with an owner
    // token.
    if (useAuthStore.getState().isStaffSession) {
      staffRefreshPromise ??= (async () => {
        const refreshToken = getStaffRefreshToken();
        if (!refreshToken) {
          throw new Error("No staff refresh token");
        }
        const { data: envelope } = await refreshClient.post<StaffRefreshApiResponse>(
          "/v1/auth/staff-session/refresh",
          { refreshToken }
        );
        if (!envelope.success || !envelope.data) {
          throw new Error(envelope.message ?? "Staff session refresh failed");
        }
        const { accessToken, role, expiresIn } = envelope.data;
        persistStaffSessionCookies(accessToken, role, expiresIn, getCookie("tenant_id"));
      })();

      try {
        await staffRefreshPromise;
        if (!originalRequest) {
          return Promise.reject(error);
        }
        originalRequest._retry = true;
        return apiClient(originalRequest);
      } catch {
        useAuthStore.getState().endStaffSession();
        if (typeof window !== "undefined") {
          window.location.replace("/select-staff");
        }
        return Promise.reject(error);
      } finally {
        staffRefreshPromise = null;
      }
    }

    refreshPromise ??= (async () => {
      try {
        const refreshToken = getCookie("refresh_token");
        const { data: envelope } = await refreshClient.post<RefreshApiResponse>(
          "/v1/auth/refresh",
          { refreshToken }
        );

        if (!envelope.success || !envelope.data) {
          throw new Error(envelope.message ?? "Token refresh failed");
        }

        const { accessToken, refreshToken: newRefreshToken, expiresIn, refreshExpiresIn } =
          envelope.data;
        const { role, tenantId } = useAuthStore.getState();

        persistAuthCookies(accessToken, newRefreshToken, expiresIn, refreshExpiresIn, role ?? "Manager", tenantId);
      } catch {
        clearAuthCookies();
        useAuthStore.getState().clearAuth();
        if (typeof window !== "undefined") {
          const from = encodeURIComponent(window.location.pathname);
          window.location.replace(`/login?from=${from}`);
        }
        throw error;
      }
    })();

    try {
      await refreshPromise;

      if (!originalRequest) {
        return Promise.reject(error);
      }

      originalRequest._retry = true;
      return apiClient(originalRequest);
    } catch {
      return Promise.reject(error);
    } finally {
      refreshPromise = null;
    }
  }
);

export default apiClient;
