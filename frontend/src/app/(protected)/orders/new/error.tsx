"use client";

export default function NewOrderError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong creating a new order.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
