export default function NewRestaurantLoading() {
  return (
    <div className="mx-auto max-w-xl space-y-6">
      <div className="h-8 w-56 animate-pulse rounded-md bg-zinc-200" />
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="space-y-1">
          <div className="h-4 w-32 animate-pulse rounded bg-zinc-200" />
          <div className="h-10 w-full animate-pulse rounded-md bg-zinc-100" />
        </div>
      ))}
    </div>
  );
}
