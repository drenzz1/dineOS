"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { exchangeKeycloakCode } from "@/lib/auth/keycloak";
import { useAuthStore } from "@/stores/authStore";

export default function AuthCallbackPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setAuth = useAuthStore((state) => state.setAuth);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const keycloakError = searchParams.get("error");
    const code = searchParams.get("code");
    const state = searchParams.get("state");

    if (keycloakError) {
      setError("Keycloak sign-in was cancelled.");
      return;
    }

    if (!code || !state) {
      setError("Keycloak did not return a valid authorization response.");
      return;
    }

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

        setError(err instanceof Error ? err.message : "Keycloak sign-in failed.");
      });

    return () => {
      cancelled = true;
    };
  }, [router, searchParams, setAuth]);

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
