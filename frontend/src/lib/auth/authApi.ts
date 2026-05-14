import apiClient from "@/lib/api/apiClient";
import axios from "axios";
import { ApiError, type ApiResponse } from "@/lib/api/envelope";

interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  refreshExpiresIn: number | null;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  refreshExpiresIn: number | null;
}

export async function logout(): Promise<void> {
  const refreshToken = document.cookie
    .split("; ")
    .find((row) => row.startsWith("refresh_token="))
    ?.split("=")[1];

  if (!refreshToken) {
    return;
  }

  try {
    await apiClient.post("/v1/auth/logout", { refreshToken: decodeURIComponent(refreshToken) });
  } catch {
    // Continue with client-side cleanup regardless of backend response
  }
}

export async function login(username: string, password: string): Promise<AuthTokens> {
  try {
    const res = await apiClient.post<ApiResponse<RefreshTokenResponse>>(
      "/v1/auth/login",
      { username, password }
    );

    const body = res.data;

    if (!body.success || !body.data) {
      const message = body.errors?.[0] ?? body.message ?? "Login failed.";
      throw new ApiError({ error: message, errors: body.errors ?? [], traceId: body.traceId, status: res.status });
    }

    return {
      accessToken: body.data.accessToken,
      refreshToken: body.data.refreshToken,
      expiresIn: body.data.expiresIn,
      refreshExpiresIn: body.data.refreshExpiresIn ?? null,
    };
  } catch (err) {
    if (err instanceof ApiError) throw err;
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as ApiResponse<unknown> | undefined;

      if (status === 400) {
        const message = body?.errors?.[0] ?? body?.message ?? "Invalid request.";
        throw new ApiError({ error: message, errors: body?.errors ?? [], status: 400 });
      }
      if (status === 401) {
        throw new ApiError({ error: "Invalid credentials.", status: 401 });
      }
      if (status === 429) {
        throw new ApiError({ error: "Too many attempts, try again later.", status: 429 });
      }
      if (status === 503) {
        throw new ApiError({ error: "Authentication service unavailable.", status: 503 });
      }
    }
    throw err;
  }
}
