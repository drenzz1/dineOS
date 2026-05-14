export default function VerifyEmailLoading() {
  return (
    <div className="flex min-h-screen items-center justify-center p-6 bg-zinc-50">
      <div className="w-full max-w-sm space-y-6 rounded-lg border border-zinc-200 bg-white p-8 shadow-sm">
        <div className="space-y-2">
          <div className="h-7 w-48 animate-pulse rounded-md bg-zinc-200" />
          <div className="h-4 w-full animate-pulse rounded bg-zinc-100" />
        </div>
        <div className="space-y-1">
          <div className="h-4 w-32 animate-pulse rounded bg-zinc-100" />
          <div className="h-10 w-full animate-pulse rounded-md bg-zinc-100" />
        </div>
        <div className="h-10 w-full animate-pulse rounded-md bg-zinc-200" />
      </div>
    </div>
  );
}
