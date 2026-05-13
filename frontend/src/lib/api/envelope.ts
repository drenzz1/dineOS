import type { AxiosResponse } from "axios";
import axios from "axios";

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: string[] | null;
  traceId?: string | null;
}

export class ApiError extends Error {
  readonly error: string;
  readonly errors: string[];
  readonly traceId: string | null;
  readonly status: number;

  constructor(opts: {
    error: string;
    errors?: string[];
    traceId?: string | null;
    status: number;
  }) {
    super(opts.error);
    this.name = "ApiError";
    this.error = opts.error;
    this.errors = opts.errors ?? [];
    this.traceId = opts.traceId ?? null;
    this.status = opts.status;
  }
}

export function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
  const body = res.data;
  if (!body.success || body.data == null) {
    throw new ApiError({
      error: body.errors?.[0] ?? body.message ?? "Request failed.",
      errors: body.errors ?? [],
      traceId: body.traceId ?? null,
      status: res.status,
    });
  }
  return body.data;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (axios.isAxiosError(error)) {
    const body = error.response?.data as ApiResponse<unknown> | undefined;
    return new ApiError({
      error: body?.errors?.[0] ?? body?.message ?? error.message,
      errors: body?.errors ?? [],
      traceId: body?.traceId ?? null,
      status: error.response?.status ?? 0,
    });
  }
  const message = error instanceof Error ? error.message : "An unexpected error occurred.";
  return new ApiError({ error: message, status: 0 });
}
