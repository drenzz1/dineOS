"use client";

export default function PaymentsError({
  unstable_retry,
}: {
  unstable_retry: () => void;
}) {
  return (
    <div>
      <p>Something went wrong on payments.</p>
      <button onClick={() => unstable_retry()}>Try again</button>
    </div>
  );
}
