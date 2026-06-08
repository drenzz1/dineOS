"use client";

import Link from "next/link";

export default function Error({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-bg px-5 text-center text-fg">
      <h1 className="text-2xl font-semibold tracking-[-0.02em]">Something went wrong.</h1>
      <p className="mt-3 max-w-sm text-[14.5px] leading-7 text-fg-muted">
        We could not load this page. Try again, or head back to the homepage.
      </p>
      <div className="mt-6 flex gap-2.5">
        <button
          type="button"
          onClick={reset}
          className="inline-flex h-[38px] items-center rounded-md bg-accent px-4 text-[13px] font-semibold text-accent-fg hover:bg-accent-hover"
        >
          Try again
        </button>
        <Link
          href="/"
          className="inline-flex h-[38px] items-center rounded-md border border-border-strong bg-surface px-4 text-[13px] font-semibold text-fg hover:bg-surface-2"
        >
          Back to home
        </Link>
      </div>
    </main>
  );
}
