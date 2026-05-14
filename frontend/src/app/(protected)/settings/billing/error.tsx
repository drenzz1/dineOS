"use client";

export default function BillingError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong loading billing.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
