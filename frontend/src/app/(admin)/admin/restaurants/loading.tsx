export default function AdminRestaurantsLoading() {
  return (
    <div className="space-y-6">
      <div className="h-8 w-48 animate-pulse rounded-md bg-surface-3" />
      <div className="flex gap-3">
        <div className="h-10 w-64 animate-pulse rounded-md bg-surface-3" />
        <div className="h-10 w-36 animate-pulse rounded-md bg-surface-3" />
      </div>
      <div className="h-64 animate-pulse rounded-lg bg-surface-2" />
    </div>
  );
}
