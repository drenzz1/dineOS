"use client";

export default function SelectStaffError({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="p-12 text-center">
      <p className="text-sm text-fg-muted">Something went wrong loading the staff roster.</p>
      <button
        onClick={() => unstable_retry()}
        className="mt-3 text-sm font-medium text-accent underline underline-offset-2"
      >
        Try again
      </button>
    </div>
  );
}
