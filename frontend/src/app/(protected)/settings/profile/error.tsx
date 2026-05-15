"use client";

export default function RestaurantProfileError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="space-y-3">
      <p className="text-[13px] text-fg">Something went wrong loading the profile.</p>
      <button
        type="button"
        onClick={() => unstable_retry()}
        className="text-[13px] underline text-accent hover:text-accent-hover"
      >
        Try again
      </button>
    </div>
  );
}
