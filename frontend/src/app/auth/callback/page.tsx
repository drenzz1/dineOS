"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { exchangeKeycloakCode } from "@/lib/auth/keycloak";
import { useAuthStore } from "@/stores/authStore";

function AuthCallback() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setAuth = useAuthStore((state) => state.setAuth);

  const keycloakError = searchParams.get("error");
  const code = searchParams.get("code");
  const state = searchParams.get("state");

  const initialError = keycloakError
    ? "Keycloak sign-in was cancelled."
    : !code || !state
      ? "Keycloak did not return a valid authorization response."
      : null;

  const [asyncError, setAsyncError] = useState<string | null>(null);
  const error = initialError ?? asyncError;

  useEffect(() => {
    if (initialError || !code || !state) return;

    let cancelled = false;

    exchangeKeycloakCode(code, state)
      .then((session) => {
        if (cancelled) return;

        setAuth(
          session.userId,
          session.role,
          session.tenantId,
          session.restaurantName,
          session.accessToken
        );
        router.replace(session.destination);
      })
      .catch((err: unknown) => {
        if (cancelled) return;

        setAsyncError(
          err instanceof Error ? err.message : "Keycloak sign-in failed."
        );
      });

    return () => {
      cancelled = true;
    };
  }, [code, state, initialError, router, setAuth]);

  return (
    <main className="flex min-h-screen items-center justify-center bg-zinc-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm">
        <h1 className="text-xl font-semibold text-zinc-900">
          {error ? "Sign-in failed" : "Signing you in"}
        </h1>
        <p className="mt-2 text-sm text-zinc-500">
          {error ?? "Finishing the Keycloak session."}
        </p>
        {error && (
          <button
            type="button"
            className="mt-6 h-10 w-full rounded-md bg-zinc-900 px-4 text-sm font-semibold text-white hover:bg-zinc-800"
            onClick={() => router.replace("/login")}
          >
            Back to sign in
          </button>
        )}
      </div>
    </main>
  );
}

export default function AuthCallbackPage() {
  return (
    <Suspense fallback={null}>
      <AuthCallback />
    </Suspense>
  );
}
