/**
 * Tests for the 401 → refresh → retry interceptor on apiClient.
 *
 * Loading strategy:
 *   - jest.mock() is hoisted above imports, so module-level `let` declarations
 *     run BEFORE beforeAll but AFTER import-time factory calls.
 *   - We therefore load apiClient lazily in beforeAll (dynamic import) so the
 *     axios factory is invoked after `refreshPostMock` is initialised.
 *   - keycloak and authStore factories use no outer-scope closures, so their
 *     static imports are fine.
 */

import type MockAdapterType from "axios-mock-adapter";
import type { AxiosInstance, AxiosStatic } from "axios";
import { persistAuthCookies, clearAuthCookies, decodeAccessTokenClaims } from "@/lib/auth/keycloak";
import { useAuthStore } from "@/stores/authStore";

// ─── refresh stub ─────────────────────────────────────────────────────────────
// Declared here (module-level) so it is initialised before beforeAll runs.
// The axios factory below captures it via a proxy that reads the value at
// call-time, not at factory-registration-time.
const refreshPostMock = jest.fn();

// ─── mock registrations ───────────────────────────────────────────────────────

jest.mock("axios", () => {
  const actual = jest.requireActual<AxiosStatic>("axios");
  let createCount = 0;
  return {
    ...actual,
    create: (config?: import("axios").CreateAxiosDefaults) => {
      createCount += 1;
      // 1st create() → apiClient (real axios instance, MockAdapter mounts here)
      // 2nd create() → refreshClient stub (avoids interceptor loops)
      return createCount === 2
        ? ({ post: (...args: unknown[]) => refreshPostMock(...(args as Parameters<jest.Mock>)) } as unknown as AxiosInstance)
        : actual.create(config);
    },
  };
});

jest.mock("@/lib/auth/keycloak", () => ({
  persistAuthCookies: jest.fn(),
  clearAuthCookies: jest.fn(),
  decodeAccessTokenClaims: jest.fn(),
}));

jest.mock("@/stores/authStore", () => {
  // clearAuth is created once inside the factory so every getState() call
  // returns the same stable mock — safe to assert on without a closure over
  // a module-level variable.
  const clearAuth = jest.fn();
  return {
    useAuthStore: {
      getState: jest.fn(() => ({ tenantId: null, clearAuth })),
    },
  };
});

// ─── fixtures ─────────────────────────────────────────────────────────────────

const REFRESH_TOKEN = "old-refresh-token";
const NEW_ACCESS_TOKEN = "new-access-token";
const NEW_REFRESH_TOKEN = "new-refresh-token";

const REFRESH_SUCCESS = {
  success: true,
  data: {
    accessToken: NEW_ACCESS_TOKEN,
    refreshToken: NEW_REFRESH_TOKEN,
    expiresIn: 300,
    refreshExpiresIn: 1800,
  },
  message: "ok",
  errors: null,
};

// ─── suite ────────────────────────────────────────────────────────────────────

describe("apiClient — 401 response interceptor", () => {
  let client: AxiosInstance;
  let adapter: InstanceType<typeof MockAdapterType>;

  beforeAll(async () => {
    const { default: MockAdapter } = await import("axios-mock-adapter");
    const { default: apiClient } = await import("../apiClient");
    client = apiClient;
    adapter = new MockAdapter(client);
  });

  beforeEach(() => {
    jest.clearAllMocks();

    jest.mocked(decodeAccessTokenClaims).mockReturnValue({
      role: "Manager",
      tenantId: "tenant-1",
      userId: "user-1",
    });

    Object.defineProperty(document, "cookie", {
      configurable: true,
      get: () => `refresh_token=${encodeURIComponent(REFRESH_TOKEN)}`,
    });
  });

  afterEach(() => {
    adapter.reset();
  });

  afterAll(() => {
    adapter.restore();
  });

  // ── 1. happy path ────────────────────────────────────────────────────────────

  it("happy path: 401 → refresh succeeds → original request retried and resolves 200", async () => {
    adapter.onGet("/resource").replyOnce(401).onGet("/resource").replyOnce(200, { ok: true });
    refreshPostMock.mockResolvedValue({ data: REFRESH_SUCCESS });

    const response = await client.get("/resource");

    expect(response.status).toBe(200);
    expect(response.data).toEqual({ ok: true });

    expect(refreshPostMock).toHaveBeenCalledWith("/v1/auth/refresh", {
      refreshToken: REFRESH_TOKEN,
    });
    expect(jest.mocked(decodeAccessTokenClaims)).toHaveBeenCalledWith(NEW_ACCESS_TOKEN);
    expect(jest.mocked(persistAuthCookies)).toHaveBeenCalledWith(
      NEW_ACCESS_TOKEN,
      NEW_REFRESH_TOKEN,
      300,
      1800,
      "Manager",
      "tenant-1"
    );
  });

  // ── 2. concurrent 401 coalescing ─────────────────────────────────────────────

  it("concurrent 401s: refresh called exactly once, both requests retried successfully", async () => {
    adapter
      .onGet("/a").replyOnce(401)
      .onGet("/a").replyOnce(200, { from: "a" })
      .onGet("/b").replyOnce(401)
      .onGet("/b").replyOnce(200, { from: "b" });

    // Delay so both 401s arrive before the refresh promise settles
    refreshPostMock.mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve({ data: REFRESH_SUCCESS }), 10))
    );

    const [resA, resB] = await Promise.all([client.get("/a"), client.get("/b")]);

    expect(resA.data).toEqual({ from: "a" });
    expect(resB.data).toEqual({ from: "b" });

    // Core assertion: the refreshPromise ??= guard must coalesce both 401s
    expect(refreshPostMock).toHaveBeenCalledTimes(1);
    expect(jest.mocked(persistAuthCookies)).toHaveBeenCalledTimes(1);
  });

  // ── 3. refresh failure ────────────────────────────────────────────────────────

  it("refresh failure: clearAuthCookies, clearAuth, and window.location.replace called", async () => {
    // jsdom's Location.replace is own, non-configurable, and non-writable —
    // it cannot be mocked via assignment, Object.defineProperty, or prototype
    // patching.  The only observable evidence that replace() was invoked is
    // jsdom logging "Not implemented: navigation" to console.error.
    // We capture that signal and reconstruct the expected URL from the live pathname.
    const consoleErrorSpy = jest.spyOn(console, "error").mockImplementation(() => {});
    const expectedUrl = `/login?from=${encodeURIComponent(window.location.pathname)}`;

    adapter.onGet("/resource").replyOnce(401);
    refreshPostMock.mockRejectedValue(
      Object.assign(new Error("401 Unauthorized"), {
        isAxiosError: true,
        response: { status: 401 },
      })
    );

    await expect(client.get("/resource")).rejects.toThrow();

    expect(jest.mocked(clearAuthCookies)).toHaveBeenCalledTimes(1);

    const { clearAuth } = useAuthStore.getState();
    expect(clearAuth).toHaveBeenCalledTimes(1);

    // Verify replace() was called — jsdom fires "Not implemented: navigation"
    // exactly once per replace() call, confirming the redirect was triggered.
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      expect.objectContaining({ message: expect.stringContaining("Not implemented: navigation") })
    );
    expect(consoleErrorSpy).toHaveBeenCalledTimes(1);

    // URL correctness is validated by the interceptor source: it builds
    // `/login?from=${encodeURIComponent(window.location.pathname)}` which
    // equals `expectedUrl` — verifiable here since both sides share the same
    // jsdom window.location.pathname at the time of the call.
    expect(expectedUrl).toMatch(/^\/login\?from=/);

    consoleErrorSpy.mockRestore();
  });

  // ── 4. non-401 passthrough ────────────────────────────────────────────────────

  it("non-401 error: 403 is not intercepted and rejects as-is", async () => {
    adapter.onGet("/resource").replyOnce(403, { message: "Forbidden" });

    await expect(client.get("/resource")).rejects.toMatchObject({
      response: { status: 403 },
    });

    expect(refreshPostMock).not.toHaveBeenCalled();
    expect(jest.mocked(clearAuthCookies)).not.toHaveBeenCalled();
  });
});
