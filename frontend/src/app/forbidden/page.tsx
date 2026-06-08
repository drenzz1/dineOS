"use client";

import { Suspense } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { getDestination } from "@/lib/auth/keycloak";

function ForbiddenContent() {
  const searchParams = useSearchParams();
  const role = useAuthStore((state) => state.role);
  const attemptedPath = searchParams.get("from");

  // Send the user somewhere they can actually go. With a known role that's their
  // home; without one (not yet hydrated / signed out) fall back to login.
  const homeHref = role ? getDestination(role, null) : "/login";
  const homeLabel = role ? "Go to your home page" : "Go to login";

  return (
    <main className="flex min-h-screen items-center justify-center bg-surface px-6">
      <div className="w-full max-w-md rounded-lg border border-border bg-surface-2 p-8 text-center">
        <span className="mx-auto inline-flex h-12 w-12 items-center justify-center rounded-full bg-accent/15 text-xl">
          🔒
        </span>
        <h1 className="mt-5 text-lg font-semibold tracking-[-0.01em] text-fg">
          You don&apos;t have access to this page
        </h1>
        <p className="mt-2 text-[13px] leading-relaxed text-fg-muted">
          {attemptedPath
            ? `Your role isn't allowed to open ${attemptedPath}.`
            : "Your role isn't allowed to open that page."}{" "}
          If you think this is a mistake, contact your manager.
        </p>
        <Link
          href={homeHref}
          className="mt-6 inline-flex h-9 items-center justify-center rounded-sm bg-accent px-4 text-[13px] font-medium text-white transition-colors duration-150 hover:bg-accent/90"
        >
          {homeLabel}
        </Link>
      </div>
    </main>
  );
}

export default function ForbiddenPage() {
  return (
    <Suspense fallback={null}>
      <ForbiddenContent />
    </Suspense>
  );
}
