"use client";

export default function VerifyEmailError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return (
    <div className="flex min-h-screen items-center justify-center p-6 bg-zinc-50">
      <div className="space-y-2 text-center">
        <p className="text-sm text-red-600">
          Failed to load verification page: {error.message}
        </p>
        <button
          onClick={unstable_retry}
          className="text-sm font-medium text-blue-600 hover:underline"
        >
          Try again
        </button>
      </div>
    </div>
  );
}
