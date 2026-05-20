import type { ToastOptions } from "@/components/ui/ToastProvider";
import { ApiError } from "@/lib/api/envelope";

type ToastFn = (opts: ToastOptions) => void;

let _toast: ToastFn | null = null;

export function registerToastFn(fn: ToastFn): void {
  _toast = fn;
}

export function handleApiError(error: unknown): void {
  if (!(error instanceof ApiError)) return;
  if (!_toast) return;

  const { status, errors, traceId, error: message } = error;

  // 401 is handled by the axios refresh interceptor (redirects to login)
  if (status === 401) return;

  const devSuffix =
    process.env.NODE_ENV === "development" && traceId ? ` [trace: ${traceId}]` : "";

  if (status === 403) {
    _toast({
      title: "Permission denied",
      description: `You don't have permission to perform this action.${devSuffix}`,
      variant: "error",
    });
    return;
  }

  if (status === 422) {
    _toast({
      title: "Validation error",
      description: (errors.length > 0 ? errors.join(" ") : message) + devSuffix,
      variant: "error",
    });
    return;
  }

  if (status === 429) {
    _toast({
      title: "Slow down",
      description: `Too many requests. Please wait a moment and try again.${devSuffix}`,
      variant: "warning",
    });
    return;
  }

  if (status === 503) {
    _toast({
      title: "Service unavailable",
      description:
        (message && message.length > 0
          ? message
          : "A required service is temporarily unavailable. Please try again in a few minutes.") +
        devSuffix,
      variant: "error",
    });
    return;
  }

  _toast({
    title: "Something went wrong",
    description: message + devSuffix,
    variant: "error",
  });
}
