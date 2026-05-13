/**
 * Integration tests for useAuthStore.login().
 *
 * Only the HTTP boundary (authApi.login, getMe) is mocked.
 * persistAuthCookies and clearAuthCookies run for real so that
 * cookie shape and store state are asserted end-to-end.
 */

import { useAuthStore } from "../authStore";
import { login as mockApiLogin } from "@/lib/auth/authApi";
import { getMe as mockApiGetMe } from "@/lib/api/meApi";
import type { MeResponse } from "@/types/me";

jest.mock("@/lib/auth/authApi");
jest.mock("@/lib/api/meApi");

const mockLogin = mockApiLogin as jest.MockedFunction<typeof mockApiLogin>;
const mockGetMe = mockApiGetMe as jest.MockedFunction<typeof mockApiGetMe>;

// ─── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Build a fake unsigned JWT whose payload contains the given claims.
 * Used here only as a token string — the payload itself is no longer
 * decoded by the auth store (data now comes from GET /api/v1/me).
 */
function makeJwt(claims: Record<string, unknown>): string {
  const b64url = (obj: Record<string, unknown>) =>
    Buffer.from(JSON.stringify(obj))
      .toString("base64")
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");
  return `${b64url({ alg: "HS256" })}.${b64url(claims)}.fakesig`;
}

function getCookie(name: string): string | null {
  const match = document.cookie
    .split("; ")
    .find((row) => row.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.split("=")[1] ?? "") : null;
}

function makeTokens(accessToken: string) {
  return {
    accessToken,
    refreshToken: "rt.opaque.token",
    expiresIn: 300,
    refreshExpiresIn: 1800,
  };
}

function meFromClaims(claims: Record<string, unknown>): MeResponse {
  return {
    id: (claims.sub as string) ?? (claims.email as string) ?? "unknown",
    email: (claims.email as string) ?? "",
    username: (claims.preferred_username as string) ?? "",
    name: (claims.name as string) ?? "",
    roles: (claims.realm_access as { roles?: string[] })?.roles ?? [],
    tenantId: claims.tenant_id ? String(claims.tenant_id) : null,
  };
}

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const MANAGER_CLAIMS = {
  sub: "user-abc",
  tenant_id: "tenant-xyz",
  realm_access: { roles: ["Manager"] },
};

const SUPERADMIN_CLAIMS = {
  sub: "admin-001",
  realm_access: { roles: ["SuperAdmin"] },
};

const MANAGER_JWT = makeJwt(MANAGER_CLAIMS);
const SUPERADMIN_JWT = makeJwt(SUPERADMIN_CLAIMS);

// ─── Setup / teardown ─────────────────────────────────────────────────────────

beforeEach(() => {
  ["access_token", "refresh_token", "role", "tenant_id"].forEach((name) => {
    document.cookie = `${name}=; max-age=0; path=/`;
  });

  localStorage.clear();

  useAuthStore.setState({
    userId: null,
    role: null,
    tenantId: null,
    restaurantName: null,
    accessToken: null,
  });
});

afterEach(() => jest.resetAllMocks());

// ─── Manager — happy path ─────────────────────────────────────────────────────

describe("useAuthStore.login — Manager success", () => {
  beforeEach(() => {
    mockLogin.mockResolvedValue(makeTokens(MANAGER_JWT));
    mockGetMe.mockResolvedValue(meFromClaims(MANAGER_CLAIMS));
  });

  it("calls authApi.login with the supplied credentials", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(mockLogin).toHaveBeenCalledWith("alice", "s3cr3t");
  });

  it("persists accessToken in the store", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(useAuthStore.getState().accessToken).toBe(MANAGER_JWT);
  });

  it("stores role from the /v1/me response", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(useAuthStore.getState().role).toBe("Manager");
  });

  it("stores tenantId from the /v1/me response", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(useAuthStore.getState().tenantId).toBe("tenant-xyz");
  });

  it("stores userId from the /v1/me response", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(useAuthStore.getState().userId).toBe("user-abc");
  });

  it("sets restaurantName to a non-null value for tenant roles", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(useAuthStore.getState().restaurantName).not.toBeNull();
  });

  it("writes the access_token cookie", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(getCookie("access_token")).toBe(MANAGER_JWT);
  });

  it("writes the refresh_token cookie from the token response", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(getCookie("refresh_token")).toBe("rt.opaque.token");
  });

  it("writes the role cookie", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(getCookie("role")).toBe("Manager");
  });

  it("writes the tenant_id cookie", async () => {
    await useAuthStore.getState().login("alice", "s3cr3t");

    expect(getCookie("tenant_id")).toBe("tenant-xyz");
  });

  it("returns the Manager default destination when from is omitted", async () => {
    const { destination } = await useAuthStore.getState().login("alice", "s3cr3t");

    expect(destination).toBe("/dashboard");
  });

  it("returns the from path when it is a valid internal path", async () => {
    const { destination } = await useAuthStore
      .getState()
      .login("alice", "s3cr3t", "/orders/42");

    expect(destination).toBe("/orders/42");
  });

  it("ignores from and uses the role default for protocol-relative URLs", async () => {
    const { destination } = await useAuthStore
      .getState()
      .login("alice", "s3cr3t", "//evil.com/steal");

    expect(destination).toBe("/dashboard");
  });
});

// ─── SuperAdmin — happy path ──────────────────────────────────────────────────

describe("useAuthStore.login — SuperAdmin success", () => {
  beforeEach(() => {
    mockLogin.mockResolvedValue(makeTokens(SUPERADMIN_JWT));
    mockGetMe.mockResolvedValue(meFromClaims(SUPERADMIN_CLAIMS));
  });

  it("stores role as SuperAdmin", async () => {
    await useAuthStore.getState().login("admin", "pass");

    expect(useAuthStore.getState().role).toBe("SuperAdmin");
  });

  it("stores tenantId as null", async () => {
    await useAuthStore.getState().login("admin", "pass");

    expect(useAuthStore.getState().tenantId).toBeNull();
  });

  it("does not write a tenant_id cookie", async () => {
    await useAuthStore.getState().login("admin", "pass");

    expect(getCookie("tenant_id")).toBeNull();
  });

  it("returns /admin/dashboard regardless of from", async () => {
    const { destination } = await useAuthStore
      .getState()
      .login("admin", "pass", "/orders");

    expect(destination).toBe("/admin/dashboard");
  });
});

// ─── Failure paths ────────────────────────────────────────────────────────────

describe("useAuthStore.login — failure", () => {
  it("propagates the error thrown by authApi.login", async () => {
    mockLogin.mockRejectedValue(new Error("Invalid credentials."));

    await expect(
      useAuthStore.getState().login("alice", "wrong")
    ).rejects.toThrow("Invalid credentials.");
  });

  it("leaves all store fields null after a failed login", async () => {
    mockLogin.mockRejectedValue(new Error("Invalid credentials."));

    await useAuthStore.getState().login("alice", "wrong").catch(() => {});

    const { userId, role, tenantId, accessToken } = useAuthStore.getState();
    expect(userId).toBeNull();
    expect(role).toBeNull();
    expect(tenantId).toBeNull();
    expect(accessToken).toBeNull();
  });

  it("does not write any cookies after a failed login", async () => {
    mockLogin.mockRejectedValue(new Error("Invalid credentials."));

    await useAuthStore.getState().login("alice", "wrong").catch(() => {});

    expect(getCookie("access_token")).toBeNull();
    expect(getCookie("refresh_token")).toBeNull();
    expect(getCookie("role")).toBeNull();
    expect(getCookie("tenant_id")).toBeNull();
  });

  it("keeps store fields null when getMe fails after successful login", async () => {
    mockLogin.mockResolvedValue(makeTokens(MANAGER_JWT));
    mockGetMe.mockRejectedValue(new Error("Unauthorized"));

    await useAuthStore.getState().login("alice", "s3cr3t").catch(() => {});

    const { userId, role, tenantId, accessToken } = useAuthStore.getState();
    expect(userId).toBeNull();
    expect(role).toBeNull();
    expect(tenantId).toBeNull();
    expect(accessToken).toBeNull();
  });
});
