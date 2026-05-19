"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { getSignupStatus } from "@/lib/api/signupApi";

const POLL_INTERVAL_MS = 3_000;
const TIMEOUT_MS = 5 * 60 * 1_000;

export default function SignupSuccessPage() {
  return (
    <Suspense fallback={<StatusShell><LoadingState /></StatusShell>}>
      <SignupSuccessInner />
    </Suspense>
  );
}

function SignupSuccessInner() {
  const searchParams = useSearchParams();
  const sessionId = searchParams.get("sessionId") ?? "";

  const startedAt = useRef<number | null>(null);
  const [timedOut, setTimedOut] = useState(false);

  const { data, isError } = useQuery({
    queryKey: ["signup-status", sessionId],
    queryFn: () => getSignupStatus(sessionId),
    enabled: Boolean(sessionId) && !timedOut,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === "Active" || status === "Failed") return false;
      if (startedAt.current !== null && Date.now() - startedAt.current >= TIMEOUT_MS) return false;
      return POLL_INTERVAL_MS;
    },
  });

  // Separate timeout effect — stops polling after 5 min without a terminal status
  useEffect(() => {
    if (startedAt.current === null) startedAt.current = Date.now();
    if (timedOut || data?.status === "Active" || data?.status === "Failed") return;
    const remaining = TIMEOUT_MS - (Date.now() - startedAt.current);
    const id = setTimeout(() => setTimedOut(true), remaining);
    return () => clearTimeout(id);
  }, [timedOut, data?.status]);

  if (!sessionId) {
    return <StatusShell>Invalid signup link. Please start over.</StatusShell>;
  }

  if (data?.status === "Active") {
    return (
      <StatusShell>
        <div className="space-y-4 text-center">
          <div className="text-4xl">🎉</div>
          <h1 className="text-xl font-semibold text-zinc-900">You&apos;re all set!</h1>
          <p className="text-sm text-zinc-500">
            Payment confirmed. Your restaurant account is ready.
          </p>
          <Link
            href="/login"
            className="inline-block rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-700"
          >
            Sign in to your account
          </Link>
        </div>
      </StatusShell>
    );
  }

  if (data?.status === "Failed" || isError) {
    return (
      <StatusShell>
        <div className="space-y-3 text-center">
          <h1 className="text-xl font-semibold text-zinc-900">Payment not completed</h1>
          <p className="text-sm text-zinc-500">
            We couldn&apos;t confirm your payment. If you were charged, contact support.
          </p>
          <a href="/signup" className="text-sm text-zinc-900 underline underline-offset-2">
            Try again
          </a>
        </div>
      </StatusShell>
    );
  }

  if (timedOut) {
    return (
      <StatusShell>
        <div className="space-y-3 text-center">
          <h1 className="text-xl font-semibold text-zinc-900">Still processing…</h1>
          <p className="text-sm text-zinc-500">
            This is taking longer than expected. Check your email for confirmation or contact support.
          </p>
        </div>
      </StatusShell>
    );
  }

  return (
    <StatusShell>
      <LoadingState />
    </StatusShell>
  );
}

function LoadingState() {
  return (
    <div className="space-y-4 text-center">
      <div className="mx-auto h-8 w-8 animate-spin rounded-full border-2 border-zinc-300 border-t-zinc-900" />
      <h1 className="text-xl font-semibold text-zinc-900">Confirming your payment…</h1>
      <p className="text-sm text-zinc-500">This usually takes just a few seconds.</p>
    </div>
  );
}

function StatusShell({ children }: { children: React.ReactNode }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-zinc-50">
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm">
        {children}
      </div>
    </main>
  );
}
