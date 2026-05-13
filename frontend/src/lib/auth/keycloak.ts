"use client";

export type AppRole = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

const ROLE_PRIORITY: AppRole[] = ["SuperAdmin", "Manager", "Cashier", "KitchenStaff"];

const ROLE_DEFAULTS: Record<AppRole, string> = {
  Manager: "/dashboard",
  Cashier: "/orders",
  KitchenStaff: "/kitchen",
  SuperAdmin: "/admin/dashboard",
};

export function getPrimaryRole(roles: string[]): AppRole {
  const role = ROLE_PRIORITY.find((candidate) => roles.includes(candidate));

  if (!role) {
    throw new Error("The access token does not include a dineOS role.");
  }

  return role;
}

function setCookie(name: string, value: string, maxAgeSeconds?: number): void {
  const maxAge =
    typeof maxAgeSeconds === "number"
      ? `; max-age=${Math.max(0, Math.floor(maxAgeSeconds))}`
      : "";

  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; samesite=lax${maxAge}`;
}

export function getDestination(role: AppRole, from: string | null): string {
  if (role === "SuperAdmin") {
    return "/admin/dashboard";
  }

  return from?.startsWith("/") && !from.startsWith("//")
    ? from
    : ROLE_DEFAULTS[role];
}

export function persistAuthCookies(
  accessToken: string,
  refreshToken: string,
  expiresIn: number,
  refreshExpiresIn: number | null,
  role: AppRole,
  tenantId: string | null
): void {
  setCookie("access_token", accessToken, expiresIn);
  setCookie("role", role, expiresIn);
  setCookie("refresh_token", refreshToken, refreshExpiresIn ?? undefined);
  if (tenantId) {
    setCookie("tenant_id", tenantId, expiresIn);
  }
}

export function clearAuthCookies(): void {
  ["access_token", "refresh_token", "role", "tenant_id"].forEach((name) => {
    document.cookie = `${name}=; path=/; max-age=0; samesite=lax`;
  });
}
