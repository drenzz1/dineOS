"use client";

export default function LoginError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on login.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
