export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-full">
      <aside className="w-64 border-r border-zinc-200 p-4">
        <nav className="flex flex-col gap-2">
          <span className="mb-4 text-xs font-semibold uppercase tracking-widest text-zinc-400">
            Admin
          </span>
          <a href="/admin/dashboard" className="rounded px-3 py-2 text-sm hover:bg-zinc-100">
            Dashboard
          </a>
          <a href="/admin/restaurants" className="rounded px-3 py-2 text-sm hover:bg-zinc-100">
            Restaurants
          </a>
        </nav>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
