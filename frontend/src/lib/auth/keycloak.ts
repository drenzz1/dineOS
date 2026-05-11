"use client";

export type AppRole = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

interface KeycloakTokenResponse {
  access_token: string;
  refresh_token?: string;
  expires_in?: number;
  refresh_expires_in?: number;
}

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

interface AuthSession {
  accessToken: string;
  refreshToken: string | null;
  userId: string;
  role: AppRole;
  tenantId: string | null;
  restaurantName: string | null;
  destination: string;
}

const CODE_VERIFIER_KEY = "dineos.pkce.code_verifier";
const STATE_KEY = "dineos.pkce.state";
const FROM_KEY = "dineos.pkce.from";
const ROLE_PRIORITY: AppRole[] = ["SuperAdmin", "Manager", "Cashier", "KitchenStaff"];

const ROLE_DEFAULTS: Record<AppRole, string> = {
  Manager: "/dashboard",
  Cashier: "/orders",
  KitchenStaff: "/kitchen",
  SuperAdmin: "/admin/dashboard",
};

function getAuthority(): string {
  return (process.env.NEXT_PUBLIC_KEYCLOAK_AUTHORITY ?? "http://localhost:8080/realms/dineos")
    .replace(/\/+$/, "");
}

function getClientId(): string {
  return process.env.NEXT_PUBLIC_KEYCLOAK_CLIENT_ID ?? "dineos-frontend";
}

function getRedirectUri(): string {
  return `${window.location.origin}/auth/callback`;
}

function base64UrlEncode(bytes: ArrayBuffer | Uint8Array): string {
  const source = bytes instanceof ArrayBuffer ? new Uint8Array(bytes) : bytes;
  let binary = "";

  source.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });

  return btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

function base64UrlDecodeJson<T>(value: string): T {
  const base64 = value
    .replace(/-/g, "+")
    .replace(/_/g, "/")
    .padEnd(Math.ceil(value.length / 4) * 4, "=");
  const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));

  return JSON.parse(new TextDecoder().decode(bytes)) as T;
}

function generateRandomValue(): string {
  const bytes = new Uint8Array(32);
  crypto.getRandomValues(bytes);
  return base64UrlEncode(bytes);
}

async function createCodeChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(verifier)
  );

  return base64UrlEncode(digest);
}

function getPrimaryRole(claims: AccessTokenClaims): AppRole {
  const roles = claims.realm_access?.roles ?? [];
  const role = ROLE_PRIORITY.find((candidate) => roles.includes(candidate));

  if (!role) {
    throw new Error("The Keycloak token does not include a dineOS role.");
  }

  return role;
}

function getDestination(role: AppRole, from: string | null): string {
  if (role === "SuperAdmin") {
    return "/admin/dashboard";
  }

  return from?.startsWith("/") && !from.startsWith("//")
    ? from
    : ROLE_DEFAULTS[role];
}

function setCookie(name: string, value: string, maxAgeSeconds?: number): void {
  const maxAge = typeof maxAgeSeconds === "number"
    ? `; max-age=${Math.max(0, Math.floor(maxAgeSeconds))}`
    : "";

  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; samesite=lax${maxAge}`;
}

export function clearAuthCookies(): void {
  ["access_token", "refresh_token", "role", "tenant_id"].forEach((name) => {
    document.cookie = `${name}=; path=/; max-age=0; samesite=lax`;
  });
}

export async function startKeycloakLogin(from?: string | null): Promise<void> {
  const verifier = generateRandomValue();
  const state = generateRandomValue();
  const challenge = await createCodeChallenge(verifier);
  const authority = getAuthority();

  sessionStorage.setItem(CODE_VERIFIER_KEY, verifier);
  sessionStorage.setItem(STATE_KEY, state);
  if (from) {
    sessionStorage.setItem(FROM_KEY, from);
  } else {
    sessionStorage.removeItem(FROM_KEY);
  }

  const params = new URLSearchParams({
    client_id: getClientId(),
    redirect_uri: getRedirectUri(),
    response_type: "code",
    scope: "openid profile email",
    code_challenge: challenge,
    code_challenge_method: "S256",
    state,
  });

  window.location.assign(`${authority}/protocol/openid-connect/auth?${params}`);
}

export async function exchangeKeycloakCode(code: string, state: string): Promise<AuthSession> {
  const expectedState = sessionStorage.getItem(STATE_KEY);
  const verifier = sessionStorage.getItem(CODE_VERIFIER_KEY);

  if (!expectedState || state !== expectedState) {
    throw new Error("The Keycloak login state is invalid.");
  }

  if (!verifier) {
    throw new Error("The Keycloak PKCE verifier is missing.");
  }

  const authority = getAuthority();
  const response = await fetch(`${authority}/protocol/openid-connect/token`, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      client_id: getClientId(),
      redirect_uri: getRedirectUri(),
      code,
      code_verifier: verifier,
    }),
  });

  if (!response.ok) {
    throw new Error("Keycloak rejected the authorization code.");
  }

  const tokens = (await response.json()) as KeycloakTokenResponse;
  const claims = base64UrlDecodeJson<AccessTokenClaims>(tokens.access_token.split(".")[1] ?? "");
  const role = getPrimaryRole(claims);
  const tenantId = claims.tenant_id === undefined || claims.tenant_id === null
    ? null
    : String(claims.tenant_id);

  if (role !== "SuperAdmin" && !tenantId) {
    throw new Error("The Keycloak token is missing the tenant_id claim.");
  }

  const from = sessionStorage.getItem(FROM_KEY);
  const accessTokenTtl = tokens.expires_in ?? 300;

  setCookie("access_token", tokens.access_token, accessTokenTtl);
  setCookie("role", role, accessTokenTtl);
  if (tokens.refresh_token) {
    setCookie("refresh_token", tokens.refresh_token, tokens.refresh_expires_in);
  }
  if (tenantId) {
    setCookie("tenant_id", tenantId, accessTokenTtl);
  }

  sessionStorage.removeItem(CODE_VERIFIER_KEY);
  sessionStorage.removeItem(STATE_KEY);
  sessionStorage.removeItem(FROM_KEY);

  return {
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token ?? null,
    userId: claims.sub ?? claims.email ?? "keycloak-user",
    role,
    tenantId,
    restaurantName: tenantId ? "Olio & Sale" : null,
    destination: getDestination(role, from),
  };
}
