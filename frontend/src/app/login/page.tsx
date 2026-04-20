"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { getShiftNotes } from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { Priority } from "@/types";

const BANNER: Record<Priority, string> = {
  info: "border-blue-200 bg-blue-50 text-blue-800",
  warning: "border-amber-200 bg-amber-50 text-amber-800",
  urgent: "border-red-200 bg-red-50 text-red-800",
};

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const { data: notes = [] } = useQuery({
    queryKey: queryKeys.shifts.list(),
    queryFn: getShiftNotes,
  });

  const latestNote = [...notes].sort((a, b) =>
    b.createdAt.localeCompare(a.createdAt)
  )[0];

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

        {latestNote && (
          <div
            className={`rounded-md border px-4 py-3 text-sm ${
              latestNote.priority
                ? BANNER[latestNote.priority]
                : "border-zinc-200 bg-zinc-50 text-zinc-700"
            }`}
          >
            <p className="font-medium">{latestNote.title}</p>
            <p className="mt-0.5 line-clamp-2">{latestNote.body}</p>
          </div>
        )}

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
