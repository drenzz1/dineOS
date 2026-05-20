"use client";

import { Button } from "@/components/ui/Button";

interface SignupSuccessErrorProps {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}

export default function SignupSuccessError({
  error,
  unstable_retry,
}: SignupSuccessErrorProps) {
  return (
    <main className="mx-auto flex min-h-screen max-w-xl items-center justify-center px-6 py-12">
      <section className="w-full rounded-2xl p-8 ring-1 ring-status-cancelled-solid/30 bg-surface">
        <h1 className="text-xl font-semibold text-fg">
          We couldn&apos;t check your signup status
        </h1>
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
