import axios, { AxiosError, InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "@/stores/authStore";
import {
  persistAuthCookies,
  clearAuthCookies,
} from "@/lib/auth/keycloak";

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
  if (token && token !== "dev") {
    config.headers["Authorization"] = `Bearer ${token}`;
  }
  return config;
});

let refreshPromise: Promise<void> | null = null;

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as RetryConfig | undefined;

    if (error.response?.status !== 401 || originalRequest?._retry) {
      return Promise.reject(error);
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
