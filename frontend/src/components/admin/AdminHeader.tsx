export default function AdminHeader() {
  return (
    <header className="flex h-16 shrink-0 items-center justify-between border-b border-slate-700/60 bg-slate-900 px-6">
      <span className="text-sm font-semibold text-slate-100">DineOS Admin</span>

      <div className="flex items-center gap-2.5">
        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-indigo-600 text-xs font-bold text-white">
          SA
        </div>
        <span className="text-sm font-medium text-slate-300">Super Admin</span>
      </div>
    </header>
  );
}
