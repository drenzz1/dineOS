"use client";

export default function FirstLoginError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong setting up your password.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
