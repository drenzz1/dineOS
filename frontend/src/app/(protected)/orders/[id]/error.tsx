"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";

export default function OrderDetailError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="rounded-lg border border-border bg-surface py-10">
      <EmptyState
        title="Something went wrong"
        description="The order detail page could not be loaded."
        cta={
          <div className="flex items-center gap-2">
            <Button onClick={() => unstable_retry()}>Try again</Button>
            <Link href="/orders">
              <Button variant="secondary">Back to orders</Button>
            </Link>
          </div>
        }
      />
    </div>
  );
}
