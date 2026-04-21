"use client";

export default function UsersError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="flex flex-col items-start gap-3">
      <p className="text-sm text-red-600">
        Failed to load users: {error.message}
      </p>
      <button
        onClick={unstable_retry}
        className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-indigo-700"
      >
        Try again
      </button>
    </div>
  );
}
