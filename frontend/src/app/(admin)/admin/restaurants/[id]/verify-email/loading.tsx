export default function VerifyEmailLoading() {
  return (
    <div className="flex min-h-screen items-center justify-center p-6 bg-surface-2">
      <div className="w-full max-w-sm space-y-6 rounded-lg border border-border bg-surface p-8 shadow-sm">
        <div className="space-y-2">
          <div className="h-7 w-48 animate-pulse rounded-md bg-surface-3" />
          <div className="h-4 w-full animate-pulse rounded bg-surface-2" />
        </div>
        <div className="space-y-1">
          <div className="h-4 w-32 animate-pulse rounded bg-surface-2" />
          <div className="h-10 w-full animate-pulse rounded-md bg-surface-2" />
        </div>
        <div className="h-10 w-full animate-pulse rounded-md bg-surface-3" />
      </div>
    </div>
  );
}
