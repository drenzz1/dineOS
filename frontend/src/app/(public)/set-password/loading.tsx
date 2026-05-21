export default function SetPasswordLoading() {
  return (
    <main className="mx-auto flex min-h-screen max-w-md items-center justify-center px-6 py-12">
      <section className="w-full rounded-2xl bg-surface p-8 ring-1 ring-border">
        <div className="h-7 w-2/3 animate-pulse rounded bg-surface-2" />
        <div className="mt-3 h-4 w-full animate-pulse rounded bg-surface-2" />
        <div className="mt-1 h-4 w-1/2 animate-pulse rounded bg-surface-2" />
        <div className="mt-8 grid gap-5">
          <div className="h-10 animate-pulse rounded bg-surface-2" />
          <div className="h-10 animate-pulse rounded bg-surface-2" />
          <div className="h-10 animate-pulse rounded bg-surface-2" />
        </div>
      </section>
    </main>
  );
}
