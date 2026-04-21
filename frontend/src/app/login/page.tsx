"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { getShiftNotes } from "@/lib/api/shiftApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { Priority, Role } from "@/types";

const BANNER: Record<Priority, string> = {
  info: "border-blue-200 bg-blue-50 text-blue-800",
  warning: "border-amber-200 bg-amber-50 text-amber-800",
  urgent: "border-red-200 bg-red-50 text-red-800",
};

const ROLE_DEFAULTS: Record<Role, string> = {
  Manager: "/dashboard",
  Cashier: "/orders",
  KitchenStaff: "/kitchen",
  SuperAdmin: "/admin/dashboard",
};

function setDevAuthCookies(role: Role): void {
  document.cookie = "access_token=dev; path=/";
  document.cookie = `role=${role}; path=/`;
}

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

  function handleDevLogin(role: Role) {
    setDevAuthCookies(role);
    const from = searchParams.get("from");
    const destination =
      role === "SuperAdmin"
        ? ROLE_DEFAULTS.SuperAdmin
        : (from ?? ROLE_DEFAULTS[role]);
    router.push(destination);
  }

  const tenantRoles: Role[] = ["Manager", "Cashier", "KitchenStaff"];

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
          Dev mode — select a role to bypass auth.
        </div>

        <div className="space-y-2">
          {tenantRoles.map((role) => (
            <Button
              key={role}
              variant="secondary"
              className="w-full"
              onClick={() => handleDevLogin(role)}
            >
              {role}
            </Button>
          ))}
        </div>

        <div className="relative">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-zinc-200" />
          </div>
          <div className="relative flex justify-center text-xs">
            <span className="bg-white px-2 text-zinc-400">or</span>
          </div>
        </div>

        <Button className="w-full" onClick={() => handleDevLogin("SuperAdmin")}>
          Sign in as SuperAdmin
        </Button>
      </div>
    </div>
  );
}
