"use client";

import Link from "next/link";

export default function Error() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-surface px-6">
      <div className="w-full max-w-md rounded-lg border border-border bg-surface-2 p-8 text-center">
        <h1 className="text-lg font-semibold tracking-[-0.01em] text-fg">
          Something went wrong
        </h1>
        <p className="mt-2 text-[13px] leading-relaxed text-fg-muted">
          We couldn&apos;t load this page. Try signing in again.
        </p>
        <Link
          href="/login"
          className="mt-6 inline-flex h-9 items-center justify-center rounded-sm bg-accent px-4 text-[13px] font-medium text-white transition-colors duration-150 hover:bg-accent/90"
        >
          Go to login
        </Link>
      </div>
    </main>
  );
}
