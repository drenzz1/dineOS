"use client";

import { Suspense, useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { getSignupStatus } from "@/lib/api/signupApi";
import { queryKeys } from "@/lib/api/queryKeys";

const SIGNUP_SESSION_KEY = "dineos.signup.lastSessionId";
const POLL_MS = 2_000;
const SOFT_CAP_MS = 30_000;

type PanelTone = "success" | "info" | "warning" | "error";

export default function SignupSuccessPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-xl items-center justify-center px-6 py-12">
      <Suspense
        fallback={
          <Panel title="Loading…" tone="info">
            <p className="text-sm text-fg-muted">Preparing your account…</p>
          </Panel>
        }
      >
        <SuccessInner />
      </Suspense>
    </main>
  );
}

function readStoredSessionId(): string | null {
  if (typeof window === "undefined") return null;
  try {
    const stored = sessionStorage.getItem(SIGNUP_SESSION_KEY);
    return stored && stored.length > 0 ? stored : null;
  } catch {
    return null;
  }
}

function SuccessInner() {
  const search = useSearchParams();
  const fromUrl = search.get("session_id");
  const [storedSessionId] = useState<string | null>(() => readStoredSessionId());
  const sessionId =
    fromUrl && fromUrl.length > 0 ? fromUrl : storedSessionId;

  const [softCapReached, setSoftCapReached] = useState(false);

  useEffect(() => {
    if (!sessionId) return;
    const handle = setTimeout(() => setSoftCapReached(true), SOFT_CAP_MS);
    return () => clearTimeout(handle);
  }, [sessionId]);

  const query = useQuery({
    queryKey: queryKeys.signup.status(sessionId ?? ""),
    queryFn: () => getSignupStatus(sessionId as string),
    enabled: Boolean(sessionId) && !softCapReached,
    refetchInterval: (q) => {
      const status = q.state.data?.status;
      if (status === "Active" || status === "Failed") return false;
      return POLL_MS;
    },
    refetchIntervalInBackground: true,
  });

  const status = query.data?.status;

  useEffect(() => {
    if (status === "Active" || status === "Failed") {
      try {
        sessionStorage.removeItem(SIGNUP_SESSION_KEY);
      } catch {
        // ignore
      }
    }
  }, [status]);

  if (!sessionId) {
    return (
      <Panel title="Missing session" tone="warning">
        <p className="text-sm text-fg-muted">
          We couldn&apos;t find your checkout session. If you completed payment,
          please refresh in a moment, or{" "}
          <Link href="/signup" className="underline underline-offset-2">
            start over
          </Link>
          .
        </p>
      </Panel>
    );
  }

  if (status === "Active") {
    return (
      <Panel title="You're in." tone="success">
        <p className="text-sm text-fg-muted">
          Your restaurant is provisioned. Check your email for a temporary
          password to complete sign-in.
        </p>
        <Link
          href="/login"
          className="mt-6 inline-flex h-[34px] items-center justify-center rounded-md bg-accent px-3 text-[13px] font-[550] text-accent-fg hover:bg-accent-hover"
        >
          Go to sign in
        </Link>
      </Panel>
    );
  }

  if (status === "Failed") {
    return (
      <Panel title="Payment didn't complete" tone="error">
        <p className="text-sm text-fg-muted">
          Something went wrong with your subscription. You can try again — no
          charge has been made.
        </p>
        <Link
          href="/signup"
          className="mt-6 inline-flex h-[34px] items-center justify-center rounded-md bg-accent px-3 text-[13px] font-[550] text-accent-fg hover:bg-accent-hover"
        >
          Try again
        </Link>
      </Panel>
    );
  }

  if (softCapReached) {
    return (
      <Panel title="Still processing…" tone="info">
        <p className="text-sm text-fg-muted">
          Your payment was received. We&apos;re finishing setting up your
          restaurant — this can take a moment.
        </p>
        <Button
          className="mt-6"
          onClick={() => {
            setSoftCapReached(false);
            void query.refetch();
          }}
        >
          Check again
        </Button>
      </Panel>
    );
  }

  return (
    <Panel title="Setting up your restaurant…" tone="info">
      <p className="text-sm text-fg-muted">
        Hang tight — this usually takes a few seconds.
      </p>
      <div className="mt-6 h-1 w-full overflow-hidden rounded-full bg-surface-2">
        <div className="h-full w-1/3 animate-pulse bg-accent" />
      </div>
    </Panel>
  );
}

interface PanelProps {
  title: string;
  tone: PanelTone;
  children: ReactNode;
}

function Panel({ title, tone, children }: PanelProps) {
  const ringByTone: Record<PanelTone, string> = {
    success: "ring-status-ready-solid/30 bg-surface",
    info: "ring-border bg-surface",
    warning: "ring-status-stalled-amber-solid/30 bg-surface",
    error: "ring-status-cancelled-solid/30 bg-surface",
  };
  return (
    <section
      className={`w-full rounded-2xl p-8 ring-1 ${ringByTone[tone]}`}
    >
      <h1 className="text-xl font-semibold text-fg">{title}</h1>
      <div className="mt-3 text-sm text-fg-muted">{children}</div>
    </section>
  );
}
