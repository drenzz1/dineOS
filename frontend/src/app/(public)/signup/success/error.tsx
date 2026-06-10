"use client";

import { RouteError } from "@/components/shared/RouteError";

export default function SignupSuccessError({
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
      title="We couldn't check your signup status"
      centered
    />
  );
}
