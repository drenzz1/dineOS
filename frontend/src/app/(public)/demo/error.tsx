"use client";

import { Button } from "@/components/ui/Button";

interface DemoErrorProps {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}

export default function DemoError({ error, unstable_retry }: DemoErrorProps) {
  return (
    <main className="mx-auto max-w-xl px-6 py-12">
      <h1 className="text-xl font-semibold text-fg">Something went wrong</h1>
      <p className="mt-2 text-sm text-fg-muted">
        {error.message || "Please try again."}
      </p>
      <Button className="mt-4" onClick={() => unstable_retry()}>
        Try again
      </Button>
    </main>
  );
}
