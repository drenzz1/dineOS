"use client";

import { RouteError } from "@/components/shared/RouteError";

export default function ForgotPasswordError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <RouteError
      error={error}
      retry={unstable_retry}
      title="Couldn't load the forgot-password page"
      centered
    />
  );
}
