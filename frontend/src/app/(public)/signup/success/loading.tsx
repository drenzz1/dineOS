export default function SignupSuccessLoading() {
  return (
    <main className="mx-auto flex min-h-screen max-w-xl items-center justify-center px-6 py-12">
      <section className="w-full rounded-2xl p-8 ring-1 ring-border bg-surface">
        <div className="h-6 w-1/2 animate-pulse rounded bg-surface-2" />
        <div className="mt-3 h-4 w-3/4 animate-pulse rounded bg-surface-2" />
        <div className="mt-6 h-1 w-full overflow-hidden rounded-full bg-surface-2">
          <div className="h-full w-1/3 animate-pulse bg-accent" />
        </div>
      </section>
    </main>
  );
}
