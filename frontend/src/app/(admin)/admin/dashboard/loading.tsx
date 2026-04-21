export default function AdminDashboardLoading() {
  return (
    <div className="animate-pulse space-y-6">
      <div className="h-8 w-40 rounded-md bg-zinc-200" />
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-24 rounded-lg bg-zinc-100" />
        ))}
      </div>
      <div className="h-64 rounded-lg bg-zinc-100" />
    </div>
  );
}
