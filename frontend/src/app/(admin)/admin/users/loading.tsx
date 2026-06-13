export default function UsersLoading() {
  return (
    <div className="animate-pulse space-y-6">
      <div className="h-8 w-32 rounded-md bg-surface-3" />
      <div className="flex gap-3">
        <div className="h-10 w-64 rounded-md bg-surface-3" />
        <div className="h-10 w-36 rounded-md bg-surface-3" />
      </div>
      <div className="h-64 rounded-lg bg-surface-2" />
    </div>
  );
}
