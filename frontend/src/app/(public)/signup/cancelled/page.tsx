import Link from "next/link";

export default function SignupCancelledPage() {
  return (
    <main
      id="main-content"
      className="mx-auto flex min-h-screen max-w-xl items-center justify-center px-6 py-12"
    >
      <section className="w-full rounded-2xl border border-border bg-surface p-8 shadow-xs">
        <h1 className="text-xl font-semibold text-fg">Payment cancelled</h1>
        <p className="mt-3 text-sm text-fg-muted">
          No charge has been made. You can restart the signup whenever
          you&apos;re ready.
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <Link
            href="/signup"
            className="inline-flex h-[34px] items-center justify-center rounded-md bg-accent px-3 text-[13px] font-[550] text-accent-fg hover:bg-accent-hover"
          >
            Back to signup
          </Link>
          <Link
            href="/"
            className="inline-flex h-[34px] items-center justify-center rounded-md border border-border-strong bg-surface px-3 text-[13px] font-[550] text-fg hover:bg-surface-2"
          >
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}
