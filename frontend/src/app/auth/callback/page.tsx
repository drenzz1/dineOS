"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";

function GoogleAuthCallbackContent() {
  const searchParams = useSearchParams();
  const completeExternalLogin = useAuthStore((state) => state.completeExternalLogin);
  const started = useRef(false);
  const [message, setMessage] = useState("Completing Google sign-in...");

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    const error = searchParams.get("error");
    const from = searchParams.get("from");
    if (error) {
      window.location.replace(`/login?error=${encodeURIComponent(error)}`);
      return;
    }

    void completeExternalLogin(from)
      .then(({ destination, role }) => {
        if (role === "SuperAdmin") {
          window.location.replace(destination);
          return;
        }

        const query = from ? `?from=${encodeURIComponent(from)}` : "";
        window.location.replace(`/select-staff${query}`);
      })
      .catch(() => {
        setMessage("Google account is not linked to a dineOS restaurant.");
        window.location.replace("/login?error=google_account_not_linked");
      });
  }, [completeExternalLogin, searchParams]);

  return (
    <main
      id="main-content"
      className="flex min-h-screen items-center justify-center bg-zinc-50 px-4"
    >
      <p className="text-sm text-zinc-600">{message}</p>
    </main>
  );
}

export default function GoogleAuthCallbackPage() {
  return (
    <Suspense
      fallback={
        <main
          id="main-content"
          className="flex min-h-screen items-center justify-center bg-zinc-50 px-4"
        >
          <p className="text-sm text-zinc-600">Completing Google sign-in...</p>
        </main>
      }
    >
      <GoogleAuthCallbackContent />
    </Suspense>
  );
}
