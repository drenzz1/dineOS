import { resolveOrderHubUrl } from "@/lib/realtime/hubUrl";

// Regression coverage for the SignalR hub-path defect: the hub lives at the
// backend's top-level /hubs/orders, so the resolved URL must never carry the
// /api prefix (which 404/405s and silently kills realtime kitchen updates).
describe("resolveOrderHubUrl", () => {
  const original = process.env.NEXT_PUBLIC_API_URL;

  afterEach(() => {
    if (original === undefined) {
      delete process.env.NEXT_PUBLIC_API_URL;
    } else {
      process.env.NEXT_PUBLIC_API_URL = original;
    }
  });

  it("resolves to a relative /hubs/orders when NEXT_PUBLIC_API_URL is unset", () => {
    delete process.env.NEXT_PUBLIC_API_URL;
    expect(resolveOrderHubUrl()).toBe("/hubs/orders");
  });

  it("strips a trailing /api so the hub is not double-prefixed", () => {
    process.env.NEXT_PUBLIC_API_URL = "http://localhost:5000/api";
    expect(resolveOrderHubUrl()).toBe("http://localhost:5000/hubs/orders");
  });

  it("strips a trailing /api/ that ends with a slash", () => {
    process.env.NEXT_PUBLIC_API_URL = "http://localhost:5001/api/";
    expect(resolveOrderHubUrl()).toBe("http://localhost:5001/hubs/orders");
  });

  it("never produces the broken /api/hubs/orders path", () => {
    process.env.NEXT_PUBLIC_API_URL = "/api";
    expect(resolveOrderHubUrl()).not.toContain("/api/hubs");
  });
});
