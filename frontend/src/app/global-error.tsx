"use client";

import { useEffect } from "react";
import "./globals.css";

/**
 * Last-resort error boundary that catches failures in the root layout itself.
 * It replaces the root layout when active, so it must render its own <html>
 * and <body> and import global styles (the layout's fonts/providers are gone).
 */
export default function GlobalError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  useEffect(() => {
    // Surface the error to the console and any attached error-reporting service.
    console.error(error);
  }, [error]);

  return (
    <html lang="en">
      <body className="min-h-screen bg-bg text-fg antialiased">
        {/* metadata exports aren't supported in global-error — set the title directly. */}
        <title>Something went wrong | dineOS</title>
        <main className="mx-auto flex min-h-screen max-w-md flex-col items-center justify-center gap-4 px-6 text-center">
          <h1 className="text-2xl font-semibold">Something went wrong</h1>
          <p className="text-sm text-fg-muted">
            dineOS hit an unexpected error and couldn&rsquo;t finish loading. You can
            try again, and if it keeps happening, contact your administrator.
          </p>
          <div className="flex items-center gap-3">
            <button
              onClick={() => unstable_retry()}
              className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-fg transition-colors hover:bg-accent-hover"
            >
              Try again
            </button>
            <button
              onClick={() => window.location.assign("/")}
              className="rounded-md border border-border-strong px-4 py-2 text-sm font-medium text-fg transition-colors hover:bg-bg-sunken"
            >
              Go home
            </button>
          </div>
          {error.digest ? (
            <p className="text-xs text-fg-subtle">Reference: {error.digest}</p>
          ) : null}
        </main>
      </body>
    </html>
  );
}
