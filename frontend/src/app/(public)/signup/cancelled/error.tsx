"use client";

import { Button } from "@/components/ui/Button";

interface SignupCancelledErrorProps {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}

export default function SignupCancelledError({
  error,
  unstable_retry,
}: SignupCancelledErrorProps) {
  return (
    <main className="mx-auto flex min-h-screen max-w-xl items-center justify-center px-6 py-12">
      <section className="w-full rounded-2xl border border-border bg-surface p-8">
        <h1 className="text-xl font-semibold text-fg">Something went wrong</h1>
        <p className="mt-3 text-sm text-fg-muted">
          {error.message || "Please try again."}
        </p>
        <Button className="mt-6" onClick={() => unstable_retry()}>
          Try again
        </Button>
      </section>
    </main>
  );
}
