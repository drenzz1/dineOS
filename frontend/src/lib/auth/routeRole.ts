export type RouteRole = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

const ROLE_VALUES: RouteRole[] = [
  "Manager",
  "Cashier",
  "KitchenStaff",
  "SuperAdmin",
];

interface TokenClaims {
  role?: unknown;
  token_use?: unknown;
  realm_access?: {
    roles?: unknown;
  };
}

function isValidRole(value: string): value is RouteRole {
  return (ROLE_VALUES as string[]).includes(value);
}

function decodeTokenClaims(token: string): TokenClaims | null {
  const payload = token.split(".")[1];
  if (!payload) return null;

  try {
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, "=");
    return JSON.parse(atob(padded)) as TokenClaims;
  } catch {
    return null;
  }
}

export function getRoleFromToken(token: string): RouteRole | null {
  const claims = decodeTokenClaims(token);
  if (!claims) return null;

  if (typeof claims.role === "string" && isValidRole(claims.role)) {
    return claims.role;
  }

  const roles = Array.isArray(claims.realm_access?.roles)
    ? claims.realm_access.roles.filter(
        (role): role is string => typeof role === "string"
      )
    : [];

  if (roles.includes("Demo")) return "Manager";
  return ROLE_VALUES.find((role) => roles.includes(role)) ?? null;
}

export function isStaffSessionToken(token: string | null): boolean {
  if (!token) return false;
  return decodeTokenClaims(token)?.token_use === "staff_session";
}

export function resolveRequestRole(
  token: string | null,
  roleCookie: string | undefined,
  sessionMode?: string
): RouteRole | null {
  const cookieRole =
    roleCookie && isValidRole(roleCookie) ? roleCookie : null;

  // During the owner -> PIN-selected staff handoff, the browser can briefly
  // send an older access_token cookie on the first document request. The
  // explicit staff marker and role were written together with the new token,
  // so they are authoritative for route gating until API authentication
  // validates or refreshes the active token.
  if (sessionMode === "staff" && cookieRole) {
    return cookieRole;
  }

  return (token ? getRoleFromToken(token) : null) ?? cookieRole;
}
