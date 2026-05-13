"use client";

export type AppRole = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

interface AccessTokenClaims {
  sub?: string;
  email?: string;
  preferred_username?: string;
  name?: string;
  tenant_id?: string | number;
  realm_access?: {
    roles?: string[];
  };
}

const ROLE_PRIORITY: AppRole[] = ["SuperAdmin", "Manager", "Cashier", "KitchenStaff"];

const ROLE_DEFAULTS: Record<AppRole, string> = {
  Manager: "/dashboard",
  Cashier: "/orders",
  KitchenStaff: "/kitchen",
  SuperAdmin: "/admin/dashboard",
};

function base64UrlDecodeJson<T>(value: string): T {
  const base64 = value
    .replace(/-/g, "+")
    .replace(/_/g, "/")
    .padEnd(Math.ceil(value.length / 4) * 4, "=");
  const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));

  return JSON.parse(new TextDecoder().decode(bytes)) as T;
}

function getPrimaryRole(claims: AccessTokenClaims): AppRole {
  const roles = claims.realm_access?.roles ?? [];
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

export function decodeAccessTokenClaims(accessToken: string): {
  userId: string;
  role: AppRole;
  tenantId: string | null;
} {
  const claims = base64UrlDecodeJson<AccessTokenClaims>(accessToken.split(".")[1] ?? "");
  const role = getPrimaryRole(claims);
  const tenantId =
    claims.tenant_id === undefined || claims.tenant_id === null
      ? null
      : String(claims.tenant_id);

  if (role !== "SuperAdmin" && !tenantId) {
    throw new Error("The access token is missing the tenant_id claim.");
  }

  return {
    userId: claims.sub ?? claims.email ?? "unknown",
    role,
    tenantId,
  };
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
