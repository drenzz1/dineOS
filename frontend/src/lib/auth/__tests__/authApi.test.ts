import { getGoogleLoginUrl, login } from "../authApi";
import apiClient from "@/lib/api/apiClient";

jest.mock("@/lib/api/apiClient", () => ({
  __esModule: true,
  default: { post: jest.fn() },
}));

const mockPost = apiClient.post as jest.MockedFunction<typeof apiClient.post>;

// Minimal valid token fixture
const TOKENS = {
  accessToken: "header.payload.sig",
  refreshToken: "refresh.token.value",
  expiresIn: 300,
  refreshExpiresIn: 1800,
};

function makeAxiosError(status: number, data: unknown = {}) {
  return Object.assign(new Error(`HTTP ${status}`), {
    isAxiosError: true,
    response: { status, data },
  });
}

afterEach(() => jest.resetAllMocks());

describe("getGoogleLoginUrl", () => {
  it("builds the backend Google auth endpoint with a safe encoded return path", () => {
    expect(getGoogleLoginUrl("/reports?period=today")).toBe(
      "/api/v1/auth/google?from=%2Freports%3Fperiod%3Dtoday"
    );
  });
});

// ─── 200 success ──────────────────────────────────────────────────────────────

describe("login — 200 success", () => {
  it("returns AuthTokens when the backend envelope is success:true", async () => {
    mockPost.mockResolvedValue({
      data: { success: true, data: TOKENS, message: null, errors: null },
    });

    const result = await login("alice", "hunter2");

    expect(mockPost).toHaveBeenCalledWith("/v1/auth/login", {
      username: "alice",
      password: "hunter2",
    });
    expect(result).toEqual(TOKENS);
  });

  it("includes refreshExpiresIn: null when the field is absent", async () => {
    const tokensNoExpiry = { ...TOKENS, refreshExpiresIn: null };
    mockPost.mockResolvedValue({
      data: { success: true, data: tokensNoExpiry, message: null, errors: null },
    });

    const result = await login("alice", "hunter2");

    expect(result.refreshExpiresIn).toBeNull();
  });

  it("throws when the envelope has success:false with an errors array", async () => {
    mockPost.mockResolvedValue({
      data: {
        success: false,
        data: null,
        message: "Validation failed.",
        errors: ["Username is required."],
      },
    });

    await expect(login("", "hunter2")).rejects.toThrow("Username is required.");
  });

  it("falls back to message when the errors array is empty", async () => {
    mockPost.mockResolvedValue({
      data: { success: false, data: null, message: "Login failed.", errors: [] },
    });

    await expect(login("alice", "wrong")).rejects.toThrow("Login failed.");
  });

  it("uses a generic fallback when both errors and message are absent", async () => {
    mockPost.mockResolvedValue({
      data: { success: false, data: null, message: null, errors: null },
    });

    await expect(login("alice", "wrong")).rejects.toThrow("Login failed.");
  });
});

// ─── 400 validation error ─────────────────────────────────────────────────────

describe("login — 400 validation error", () => {
  it("throws the first errors[] entry from the ApiResponse body", async () => {
    mockPost.mockRejectedValue(
      makeAxiosError(400, {
        success: false,
        data: null,
        message: null,
        errors: ["Password must not be empty."],
      })
    );

    await expect(login("alice", "")).rejects.toThrow("Password must not be empty.");
  });

  it("falls back to message when errors[] is absent", async () => {
    mockPost.mockRejectedValue(
      makeAxiosError(400, {
        success: false,
        data: null,
        message: "Invalid request body.",
        errors: null,
      })
    );

    await expect(login("alice", "")).rejects.toThrow("Invalid request body.");
  });

  it("uses generic fallback when the 400 body is empty", async () => {
    mockPost.mockRejectedValue(makeAxiosError(400, {}));

    await expect(login("alice", "")).rejects.toThrow("Invalid request.");
  });
});

// ─── 401 bad credentials ──────────────────────────────────────────────────────

describe("login — 401 bad credentials", () => {
  it("throws the fixed 'Invalid credentials.' message", async () => {
    mockPost.mockRejectedValue(makeAxiosError(401));

    await expect(login("alice", "wrongpassword")).rejects.toThrow(
      "Invalid credentials."
    );
  });

  it("does NOT expose backend body details on a 401", async () => {
    mockPost.mockRejectedValue(
      makeAxiosError(401, { message: "account locked" })
    );

    const err = await login("alice", "wrong").catch((e: Error) => e);
    expect((err as Error).message).toBe("Invalid credentials.");
  });
});

// ─── 429 rate limit ───────────────────────────────────────────────────────────

describe("login — 429 rate limit", () => {
  it("throws the fixed rate-limit message", async () => {
    mockPost.mockRejectedValue(makeAxiosError(429));

    await expect(login("alice", "pass")).rejects.toThrow(
      "Too many attempts, try again later."
    );
  });
});

// ─── 503 IDP unavailable ──────────────────────────────────────────────────────

describe("login — 503 IDP unavailable", () => {
  it("throws the fixed service-unavailable message", async () => {
    mockPost.mockRejectedValue(makeAxiosError(503));

    await expect(login("alice", "pass")).rejects.toThrow(
      "Authentication service unavailable."
    );
  });
});

// ─── non-HTTP errors ──────────────────────────────────────────────────────────

describe("login — non-HTTP errors", () => {
  it("re-throws a TypeError (e.g. network offline) unchanged", async () => {
    mockPost.mockRejectedValue(new TypeError("Network Error"));

    await expect(login("alice", "pass")).rejects.toThrow("Network Error");
  });

  it("re-throws an unexpected 500 Axios error without swallowing it", async () => {
    mockPost.mockRejectedValue(makeAxiosError(500));

    await expect(login("alice", "pass")).rejects.toMatchObject({
      message: "HTTP 500",
    });
  });
});
