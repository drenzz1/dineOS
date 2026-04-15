export default function ProtectedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-full">
      <aside className="w-64 border-r border-zinc-200 p-4">Sidebar placeholder</aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
