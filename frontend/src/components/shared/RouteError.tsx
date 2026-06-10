"use client";

import { useEffect, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";

interface RouteErrorProps {
  error: Error & { digest?: string };
  /** Next.js `unstable_retry` — re-fetches and re-renders the failed segment. */
  retry: () => void;
  title?: string;
  /** Center as a full-height block — for public/standalone pages without the app shell. */
  centered?: boolean;
  /** Optional secondary action (e.g. a "Back to …" link) rendered beside "Try again". */
  action?: ReactNode;
}

/**
 * Shared fallback UI for route-segment `error.tsx` boundaries. Logs the error
 * (so failures surface in the console / any attached reporting service) and
 * offers a retry, with styling consistent with the rest of the app.
 */
export function RouteError({
  error,
  retry,
  title = "Something went wrong",
  centered = false,
  action,
}: RouteErrorProps) {
  useEffect(() => {
    // Surface the error to the console and any attached error-reporting service.
    console.error(error);
  }, [error]);

  return (
    <div
      role="alert"
      className={
        centered
          ? "flex min-h-[60vh] flex-col items-center justify-center gap-3 px-6 text-center"
          : "flex flex-col items-start gap-3 py-8"
      }
    >
      <h2 className="text-base font-semibold text-fg">{title}</h2>
      <p className="max-w-md text-sm text-fg-muted">
        {error.message || "An unexpected error occurred. Please try again."}
      </p>
      <div className="flex items-center gap-2">
        <Button variant="primary" onClick={() => retry()}>
          Try again
        </Button>
        {action}
      </div>
      {error.digest ? (
        <p className="text-xs text-fg-subtle">Reference: {error.digest}</p>
      ) : null}
    </div>
  );
}
