"use client";

export default function AdminRestaurantsError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on admin restaurants.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
