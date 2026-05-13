import apiClient from "@/lib/api/apiClient";
import axios from "axios";

interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  refreshExpiresIn: number | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: string[] | null;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  refreshExpiresIn: number | null;
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
      throw new Error(message);
    }

    return {
      accessToken: body.data.accessToken,
      refreshToken: body.data.refreshToken,
      expiresIn: body.data.expiresIn,
      refreshExpiresIn: body.data.refreshExpiresIn ?? null,
    };
  } catch (err) {
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as ApiResponse<unknown> | undefined;

      if (status === 400) {
        const message = body?.errors?.[0] ?? body?.message ?? "Invalid request.";
        throw new Error(message);
      }
      if (status === 401) {
        throw new Error("Invalid credentials.");
      }
      if (status === 429) {
        throw new Error("Too many attempts, try again later.");
      }
      if (status === 503) {
        throw new Error("Authentication service unavailable.");
      }
    }
    throw err;
  }
}
