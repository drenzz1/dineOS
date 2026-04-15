"use client";

export default function MenuError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on menu.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
