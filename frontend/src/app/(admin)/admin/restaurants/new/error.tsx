"use client";

import { RouteError } from "@/components/shared/RouteError";

export default function NewRestaurantError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <RouteError error={error} retry={unstable_retry} title="Couldn't load the onboarding form" />
  );
}
