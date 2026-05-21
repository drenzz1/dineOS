export default function DemoLoading() {
  return (
    <main className="mx-auto grid min-h-screen max-w-5xl gap-10 px-6 py-12 md:grid-cols-[1fr_22rem]">
      <section>
        <div className="h-8 w-2/3 animate-pulse rounded bg-surface-2" />
        <div className="mt-3 h-4 w-1/2 animate-pulse rounded bg-surface-2" />
        <div className="mt-8 grid gap-5">
          <div className="h-10 animate-pulse rounded bg-surface-2" />
          <div className="h-10 animate-pulse rounded bg-surface-2" />
          <div className="h-10 animate-pulse rounded bg-surface-2" />
        </div>
      </section>
      <aside className="h-fit rounded-2xl border border-border bg-surface-2 p-6">
        <div className="h-6 w-1/3 animate-pulse rounded bg-surface" />
        <div className="mt-3 h-4 w-1/2 animate-pulse rounded bg-surface" />
      </aside>
    </main>
  );
}
