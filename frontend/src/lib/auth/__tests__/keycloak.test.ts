import { getDestination, getPrimaryRole } from "../keycloak";

describe("getPrimaryRole", () => {
  it("returns Manager when the token carries only the Demo composite role", () => {
    expect(getPrimaryRole(["Demo"])).toBe("Manager");
  });

  it("returns Manager when Demo is present alongside other roles", () => {
    expect(getPrimaryRole(["Demo", "Cashier"])).toBe("Manager");
  });

  it("returns SuperAdmin when SuperAdmin is present (highest priority)", () => {
    expect(getPrimaryRole(["SuperAdmin", "Manager"])).toBe("SuperAdmin");
  });

  it("returns Manager when Manager is present", () => {
    expect(getPrimaryRole(["Manager"])).toBe("Manager");
  });

  it("returns Cashier when only Cashier is present", () => {
    expect(getPrimaryRole(["Cashier"])).toBe("Cashier");
  });

  it("returns KitchenStaff when only KitchenStaff is present", () => {
    expect(getPrimaryRole(["KitchenStaff"])).toBe("KitchenStaff");
  });

  it("throws when no dineOS role is present", () => {
    expect(() => getPrimaryRole([])).toThrow(
      "The access token does not include a dineOS role.",
    );
    expect(() => getPrimaryRole(["offline_access"])).toThrow();
  });
});

describe("getDestination", () => {
  it("ignores `from` for SuperAdmin and always returns /admin/dashboard", () => {
    expect(getDestination("SuperAdmin", "/orders")).toBe("/admin/dashboard");
  });

  it("returns the role default when `from` is null", () => {
    expect(getDestination("Manager", null)).toBe("/dashboard");
    expect(getDestination("Cashier", null)).toBe("/orders");
    expect(getDestination("KitchenStaff", null)).toBe("/kitchen");
  });

  it("respects internal `from` paths", () => {
    expect(getDestination("Manager", "/reports/42")).toBe("/reports/42");
  });

  it("rejects protocol-relative URLs to prevent open redirects", () => {
    expect(getDestination("Manager", "//evil.com")).toBe("/dashboard");
  });

  it("rejects external URLs", () => {
    expect(getDestination("Manager", "https://evil.com")).toBe("/dashboard");
  });
});
