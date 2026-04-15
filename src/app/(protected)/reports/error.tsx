"use client";

export default function ReportsError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on reports.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
