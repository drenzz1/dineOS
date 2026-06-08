export default function Loading() {
  return (
    <main className="min-h-screen bg-bg text-fg">
      <div className="h-[60px] border-b border-border bg-bg" />
      <div className="mx-auto max-w-3xl px-5 py-16 md:px-8 md:py-20">
        <div className="h-3 w-24 animate-pulse rounded bg-surface-3" />
        <div className="mt-4 h-10 w-3/4 animate-pulse rounded bg-surface-3" />
        <div className="mt-5 h-4 w-full animate-pulse rounded bg-surface-3" />
        <div className="mt-2 h-4 w-2/3 animate-pulse rounded bg-surface-3" />
        <div className="mt-10 space-y-3">
          <div className="h-5 w-40 animate-pulse rounded bg-surface-3" />
          <div className="h-4 w-full animate-pulse rounded bg-surface-3" />
          <div className="h-4 w-5/6 animate-pulse rounded bg-surface-3" />
        </div>
      </div>
    </main>
  );
}
