import {
  getRoleFromToken,
  resolveRequestRole,
} from "@/lib/auth/routeRole";

function jwt(payload: object): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value))
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/g, "");

  return `${encode({ alg: "none", typ: "JWT" })}.${encode(payload)}.`;
}

describe("middleware role resolution", () => {
  it("reads a staff-session role from the access token when the role cookie is missing", () => {
    const token = jwt({ role: "KitchenStaff" });

    expect(resolveRequestRole(token, undefined)).toBe("KitchenStaff");
  });

  it("prefers the access-token role over a stale role cookie", () => {
    const token = jwt({ role: "Cashier" });

    expect(resolveRequestRole(token, "Manager")).toBe("Cashier");
  });

  it("maps a demo Keycloak token to the manager route role", () => {
    const token = jwt({ realm_access: { roles: ["Demo"] } });

    expect(getRoleFromToken(token)).toBe("Manager");
  });

  it("falls back to a valid role cookie when the token cannot be decoded", () => {
    expect(resolveRequestRole("not-a-jwt", "Manager")).toBe("Manager");
  });

  it("uses the PIN-selected role during staff handoff even with a stale owner token", () => {
    const staleOwnerToken = jwt({
      realm_access: { roles: ["Demo", "Manager"] },
    });

    expect(
      resolveRequestRole(staleOwnerToken, "KitchenStaff", "staff")
    ).toBe("KitchenStaff");
  });

  it("resolves a recoverable staff session when the access token is missing", () => {
    expect(resolveRequestRole(null, "Cashier", "staff")).toBe("Cashier");
  });
});
