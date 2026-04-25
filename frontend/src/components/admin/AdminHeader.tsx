export default function AdminHeader() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-surface px-6">
      <div className="flex items-center gap-2">
        <span className="text-[13px] font-semibold text-fg">dineOS</span>
        <span className="inline-flex items-center rounded-full bg-surface-2 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.08em] text-fg-muted border border-border">
          Platform
        </span>
      </div>

      <div className="flex items-center gap-2.5">
        <div className="flex h-7 w-7 items-center justify-center rounded-full bg-accent text-[11px] font-bold text-accent-fg">
          SA
        </div>
        <span className="text-[13px] font-medium text-fg-muted">Super Admin</span>
      </div>
    </header>
  );
}
