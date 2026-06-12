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

/**
 * Thrown when the backend signals the user must rotate their temporary
 * password before standard login can succeed (tenant owners auto-provisioned
 * after Stripe checkout, #205). The FE catches this in the login form and
 * redirects to /first-login.
 */
export class FirstLoginRequiredError extends Error {
  public readonly email: string;
  constructor(email: string) {
    super("First-login password change required.");
    this.name = "FirstLoginRequiredError";
    this.email = email;
  }
}

export function getGoogleLoginUrl(from: string | null): string {
  const apiBase = (process.env.NEXT_PUBLIC_API_URL ?? "/api").replace(/\/+$/, "");
  const query = from ? `?from=${encodeURIComponent(from)}` : "";
  return `${apiBase}/v1/auth/google${query}`;
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
      if (status === 409) {
        // Backend signal: tenant owner must complete first-login password change (#205).
        throw new FirstLoginRequiredError(username);
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

export async function changePassword(
  currentPassword: string,
  newPassword: string
): Promise<void> {
  try {
    await apiClient.post("/v1/auth/change-password", { currentPassword, newPassword });
  } catch (err) {
    if (err instanceof ApiError) throw err;
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as ApiResponse<unknown> | undefined;
      const message = body?.errors?.[0] ?? body?.message ?? "Password change failed.";
      if (status === 400) throw new ApiError({ error: message, errors: body?.errors ?? [], status: 400 });
      if (status === 401) throw new ApiError({ error: "Current password is incorrect.", status: 401 });
      if (status === 429) throw new ApiError({ error: "Too many attempts, try again later.", status: 429 });
    }
    throw err;
  }
}

/**
 * Requests a password-reset code for the given email (forgot password).
 * The backend always answers 200 with the same message whether or not an
 * account exists, so success here only means "the request was accepted".
 */
export async function requestPasswordReset(email: string): Promise<void> {
  try {
    await apiClient.post("/v1/auth/forgot-password", { email });
  } catch (err) {
    if (err instanceof ApiError) throw err;
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as ApiResponse<unknown> | undefined;
      const message = body?.errors?.[0] ?? body?.message ?? "Could not send the reset code.";
      if (status === 400) throw new ApiError({ error: message, errors: body?.errors ?? [], status: 400 });
      if (status === 429) throw new ApiError({ error: "Too many attempts, try again later.", status: 429 });
    }
    throw err;
  }
}

/** Completes a forgot-password reset with the emailed 6-digit code. */
export async function resetForgottenPassword(
  email: string,
  code: string,
  newPassword: string
): Promise<void> {
  try {
    await apiClient.post("/v1/auth/reset-password", { email, code, newPassword });
  } catch (err) {
    if (err instanceof ApiError) throw err;
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      const body = err.response?.data as ApiResponse<unknown> | undefined;
      const message = body?.errors?.[0] ?? body?.message ?? "Password reset failed.";
      if (status === 400) throw new ApiError({ error: message, errors: body?.errors ?? [], status: 400 });
      if (status === 401) throw new ApiError({ error: message, errors: body?.errors ?? [], status: 401 });
      if (status === 429) throw new ApiError({ error: "Too many attempts, try again later.", status: 429 });
    }
    throw err;
  }
}

export async function firstLoginPasswordChange(
  email: string,
  currentPassword: string,
  newPassword: string
): Promise<AuthTokens> {
  try {
    const res = await apiClient.post<ApiResponse<RefreshTokenResponse>>(
      "/v1/auth/first-login-password-change",
      { email, currentPassword, newPassword }
    );

    const body = res.data;
    if (!body.success || !body.data) {
      const message = body.errors?.[0] ?? body.message ?? "Password change failed.";
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
      const message = body?.errors?.[0] ?? body?.message ?? "Password change failed.";
      if (status === 400) throw new ApiError({ error: message, errors: body?.errors ?? [], status: 400 });
      if (status === 401) throw new ApiError({ error: message || "Invalid email or temporary password.", status: 401 });
      if (status === 429) throw new ApiError({ error: "Too many attempts, try again later.", status: 429 });
      if (status === 503) throw new ApiError({ error: "Authentication service unavailable.", status: 503 });
    }
    throw err;
  }
}
