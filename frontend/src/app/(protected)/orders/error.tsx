"use client";

export default function OrdersError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on orders.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
