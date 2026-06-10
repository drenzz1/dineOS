"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { RouteError } from "@/components/shared/RouteError";

export default function OrderDetailError({
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
      title="Couldn't load this order"
      action={
        <Link href="/orders">
          <Button variant="secondary">Back to orders</Button>
        </Link>
      }
    />
  );
}
