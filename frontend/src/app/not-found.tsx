import Link from "next/link";

/**
 * Root 404. Renders within the root layout, so it inherits the app fonts and
 * styles. Catches both explicit `notFound()` calls and any unmatched URL.
 */
export default function NotFound() {
  return (
    <main className="mx-auto flex min-h-[70vh] max-w-md flex-col items-center justify-center gap-4 px-6 text-center">
      <p className="text-5xl font-semibold tracking-tight text-fg">404</p>
      <h1 className="text-lg font-semibold text-fg">Page not found</h1>
      <p className="text-sm text-fg-muted">
        The page you&rsquo;re looking for doesn&rsquo;t exist or may have been moved.
      </p>
      <Link
        href="/"
        className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-fg transition-colors hover:bg-accent-hover"
      >
        Back to home
      </Link>
    </main>
  );
}
