"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { clearAuthCookies } from "@/lib/auth/keycloak";
import { useAuthStore } from "@/stores/authStore";
import type { Role } from "@/types";

type AppRole = Role | "SuperAdmin";

const ROLE_DEFAULTS: Record<AppRole, string> = {
  Manager: "/dashboard",
  Cashier: "/orders",
  KitchenStaff: "/kitchen",
  SuperAdmin: "/admin/dashboard",
};

function setDevAuthCookies(role: AppRole): void {
  clearAuthCookies();
  document.cookie = "access_token=dev; path=/";
  document.cookie = `role=${role}; path=/`;
  document.cookie = "refresh_token=dev; path=/";
}

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setAuth = useAuthStore((state) => state.setAuth);

  function handleDevLogin(role: AppRole) {
    setDevAuthCookies(role);
    setAuth("dev-user", role, role === "SuperAdmin" ? null : "demo-tenant", "Olio & Sale", "dev");
    const from = searchParams.get("from");
    const destination =
      role === "SuperAdmin"
        ? ROLE_DEFAULTS.SuperAdmin
        : (from ?? ROLE_DEFAULTS[role]);
    router.push(destination);
  }

  const tenantRoles: AppRole[] = ["Manager", "Cashier", "KitchenStaff"];

  return (
    <main id="main-content" className="flex min-h-screen items-center justify-center bg-zinc-50">
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">Sign in to dineOS</h1>
          <p className="mt-1 text-sm text-zinc-500">
            Use your restaurant account.
          </p>
        </div>

        <div data-testid="login-role-select" className="space-y-2">
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

        <Button variant="secondary" className="w-full" onClick={() => handleDevLogin("SuperAdmin")}>
          Sign in as SuperAdmin
        </Button>
      </div>
    </main>
  );
}
