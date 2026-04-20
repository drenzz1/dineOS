"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/Button";

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();

  function handleDevLogin() {
    document.cookie = "access_token=dev; path=/";
    const from = searchParams.get("from") ?? "/orders";
    router.push(from);
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-zinc-50">
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">Sign in to dineOS</h1>
          <p className="mt-1 text-sm text-zinc-500">
            Keycloak integration coming soon.
          </p>
        </div>
        <div className="rounded-md border border-yellow-200 bg-yellow-50 px-4 py-3 text-sm text-yellow-800">
          Dev mode — click below to bypass auth.
        </div>
        <Button className="w-full" onClick={handleDevLogin}>
          Dev login
        </Button>
      </div>
    </div>
  );
}
