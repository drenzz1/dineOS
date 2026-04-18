"use client";

export default function AdminDashboardError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on admin dashboard.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
