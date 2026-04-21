"use client";

export default function RestaurantDetailError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="space-y-2">
      <p className="text-sm text-red-600">
        Failed to load restaurant: {error.message}
      </p>
      <button
        onClick={unstable_retry}
        className="text-sm font-medium text-blue-600 hover:underline"
      >
        Try again
      </button>
    </div>
  );
}
