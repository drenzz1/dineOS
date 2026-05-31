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
  // The "Demo" realm role is composite-over-Manager in Keycloak (#216);
  // Keycloak doesn't expand composites into realm_access.roles in the JWT,
  // so the frontend resolves it explicitly here.
  if (roles.includes("Demo")) {
    return "Manager";
  }

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

// Accept only same-origin internal paths: a single leading `/` followed by a
// character that is NOT `/` or `\`. This rejects:
//   - protocol-relative URLs (`//evil.com`)
//   - absolute URLs (`https://evil.com`) — they don't start with `/`
//   - backslash variants browsers normalize (`/\evil.com`, `/\\evil.com`)
//   - empty / non-string input
const SAFE_INTERNAL_PATH = /^\/(?![/\\])/;

export function getDestination(role: AppRole, from: string | null): string {
  if (role === "SuperAdmin") {
    return "/admin/dashboard";
  }

  return typeof from === "string" && SAFE_INTERNAL_PATH.test(from)
    ? from
    : ROLE_DEFAULTS[role];
}

// Writes only the access_token cookie. Used during login to authorize the
// bootstrap /me + profile calls BEFORE the role is known (and thus before the
// full cookie set can be written via persistAuthCookies). The apiClient
// request interceptor reads this cookie to attach the Authorization header.
export function persistAccessTokenCookie(
  accessToken: string,
  expiresIn?: number
): void {
  setCookie("access_token", accessToken, expiresIn);
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

// The business (Keycloak/Owner) token is retained separately so the app can
// (a) start a staff session — POST /auth/staff-session requires the Keycloak
// scheme, not a staff-session token — and (b) restore "owner mode" when a staff
// session ends. The active operational token lives in `access_token` and is
// swapped to the staff-session token after a PIN is entered (#staff-pin-auth
// Phase 3).
export function persistBusinessToken(accessToken: string, expiresIn?: number): void {
  setCookie("business_token", accessToken, expiresIn);
}

export function getBusinessToken(): string | null {
  if (typeof document === "undefined") return null;
  const cookie = document.cookie
    .split("; ")
    .find((row) => row.startsWith("business_token="));
  return cookie ? decodeURIComponent(cookie.split("=")[1] ?? "") : null;
}

// Swaps the active operational token + role to a started staff session. Leaves
// `business_token` intact (owner credential) and `refresh_token` untouched.
export function persistStaffSessionCookies(
  accessToken: string,
  role: AppRole,
  expiresIn: number,
  tenantId: string | null
): void {
  setCookie("access_token", accessToken, expiresIn);
  setCookie("role", role, expiresIn);
  if (tenantId) {
    setCookie("tenant_id", tenantId, expiresIn);
  }
}

export function persistRoleCookie(role: AppRole, expiresIn?: number): void {
  setCookie("role", role, expiresIn);
}

// The staff refresh token (longer-lived than the access token) lets the
// apiClient silently exchange an expired staff access token for a new one
// without a re-PIN (#staff-pin-auth refresh).
export function persistStaffRefreshToken(token: string, expiresIn?: number): void {
  setCookie("staff_refresh_token", token, expiresIn);
}

export function getStaffRefreshToken(): string | null {
  if (typeof document === "undefined") return null;
  const cookie = document.cookie
    .split("; ")
    .find((row) => row.startsWith("staff_refresh_token="));
  return cookie ? decodeURIComponent(cookie.split("=")[1] ?? "") : null;
}

export function clearStaffRefreshToken(): void {
  document.cookie = `staff_refresh_token=; path=/; max-age=0; samesite=lax`;
}

export function clearAuthCookies(): void {
  ["access_token", "refresh_token", "role", "tenant_id", "business_token", "staff_refresh_token"].forEach(
    (name) => {
      document.cookie = `${name}=; path=/; max-age=0; samesite=lax`;
    }
  );
}
