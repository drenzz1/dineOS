export default function RestaurantDetailLoading() {
  return (
    <div className="space-y-8">
      <div className="flex items-start justify-between">
        <div className="space-y-1">
          <div className="h-8 w-48 animate-pulse rounded-md bg-surface-3" />
          <div className="h-4 w-24 animate-pulse rounded bg-surface-2" />
        </div>
        <div className="h-10 w-28 animate-pulse rounded-md bg-surface-3" />
      </div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
        {Array.from({ length: 7 }).map((_, i) => (
          <div key={i} className="h-20 animate-pulse rounded-lg bg-surface-2" />
        ))}
      </div>
      <div className="h-16 animate-pulse rounded-lg bg-surface-2" />
      <div className="h-16 animate-pulse rounded-lg bg-surface-2" />
    </div>
  );
}
