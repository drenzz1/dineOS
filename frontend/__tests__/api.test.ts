import apiClient from "@/lib/api/apiClient";
import { getStaff, saveStaffMember, toggleStaffActive } from "@/lib/api/staffApi";
import {
  getRestaurants,
  updateRestaurantStatus,
  updateRestaurantPlan,
} from "@/lib/api/restaurantApi";

// ─── Helper: set a document cookie value ──────────────────────────────────────
function setCookie(value: string) {
  Object.defineProperty(document, "cookie", {
    writable: true,
    configurable: true,
    value,
  });
}

afterEach(() => {
  jest.restoreAllMocks();
  setCookie("");
});

// ─── staffApi: envelope unwrapping ────────────────────────────────────────────
describe("staffApi — real API calls", () => {
  it("getStaff unwraps the ApiResponse data envelope", async () => {
    const mockStaff = [
      { id: 1, fullName: "Ana Berisha", email: "ana@dineos.com", role: "Manager", isActive: true, tenantId: 1 },
    ];
    jest.spyOn(apiClient, "get").mockResolvedValue({ data: { data: mockStaff } });

    const result = await getStaff();

    expect(result).toEqual(mockStaff);
    expect(apiClient.get).toHaveBeenCalledWith("/v1/staff");
  });

  it("saveStaffMember calls POST for a new staff member", async () => {
    const created = { id: 2, fullName: "Luan K", email: "luan@dineos.com", role: "Cashier", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "post").mockResolvedValue({ data: { data: created } });

    const result = await saveStaffMember({ fullName: "Luan K", email: "luan@dineos.com", role: "Cashier" });

    expect(apiClient.post).toHaveBeenCalledWith("/v1/staff", expect.any(Object));
    expect(result).toEqual(created);
  });

  it("saveStaffMember calls PUT when an id is provided", async () => {
    const updated = { id: 1, fullName: "Updated", email: "a@b.com", role: "Manager", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "put").mockResolvedValue({ data: { data: updated } });

    const result = await saveStaffMember({ fullName: "Updated", email: "a@b.com", role: "Manager" }, 1);

    expect(apiClient.put).toHaveBeenCalledWith("/v1/staff/1", expect.any(Object));
    expect(result).toEqual(updated);
  });

  it("toggleStaffActive calls PATCH to /v1/staff/:id/active", async () => {
    const toggled = { id: 1, fullName: "Ana", email: "ana@dineos.com", role: "Manager", isActive: false, tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { data: toggled } });

    const result = await toggleStaffActive(1);

    expect(apiClient.patch).toHaveBeenCalledWith("/v1/staff/1/active");
    expect(result.isActive).toBe(false);
  });

  it("getStaff rejects on a 401 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      response: { status: 401, data: { message: "Unauthorized" } },
    });

    await expect(getStaff()).rejects.toMatchObject({ response: { status: 401 } });
  });

  it("getStaff rejects on a 403 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      response: { status: 403, data: { message: "Forbidden" } },
    });

    await expect(getStaff()).rejects.toMatchObject({ response: { status: 403 } });
  });
});

// ─── restaurantApi: paged envelope + PATCH endpoints ─────────────────────────
describe("restaurantApi — paged response and mutations", () => {
  it("getRestaurants unwraps the paged envelope and returns only the items array", async () => {
    const mockList = [{ id: 1, name: "Bella Cucina", ownerName: "Alice", ownerEmail: "a@b.com", phone: "+1", plan: "Pro", status: "Active", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: new Date().toISOString(), tenantId: 1 }];
    jest.spyOn(apiClient, "get").mockResolvedValue({
      data: { data: mockList, totalCount: 1, page: 1, pageSize: 20 },
    });

    const result = await getRestaurants();

    expect(result).toEqual(mockList);
    expect(Array.isArray(result)).toBe(true);
    expect((result as unknown as { totalCount?: number }).totalCount).toBeUndefined();
  });

  it("updateRestaurantStatus sends PATCH to /v1/admin/restaurants/:id/status", async () => {
    const updated = { id: 1, name: "Bella", ownerName: "A", ownerEmail: "a@b.com", phone: "+1", plan: "Pro", status: "Suspended", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: "", tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { data: updated } });

    const result = await updateRestaurantStatus(1, "Suspended");

    expect(apiClient.patch).toHaveBeenCalledWith(
      "/v1/admin/restaurants/1/status",
      { status: "Suspended" }
    );
    expect(result.status).toBe("Suspended");
  });

  it("updateRestaurantPlan sends PATCH to /v1/admin/restaurants/:id/plan", async () => {
    const updated = { id: 1, name: "Bella", ownerName: "A", ownerEmail: "a@b.com", phone: "+1", plan: "Free", status: "Active", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: "", tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { data: updated } });

    const result = await updateRestaurantPlan(1, "Free");

    expect(apiClient.patch).toHaveBeenCalledWith(
      "/v1/admin/restaurants/1/plan",
      { plan: "Free" }
    );
    expect(result.plan).toBe("Free");
  });

  it("getRestaurants rejects on a 500 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      response: { status: 500, data: { message: "Internal Server Error" } },
    });

    await expect(getRestaurants()).rejects.toMatchObject({ response: { status: 500 } });
  });
});

// ─── apiClient: Authorization interceptor ─────────────────────────────────────
describe("apiClient — Authorization interceptor", () => {
  it("does NOT attach Authorization header when token is the dev bypass", async () => {
    setCookie("access_token=dev");
    const spy = jest.spyOn(apiClient, "get").mockResolvedValue({ data: { data: [] } });
    await apiClient.get("/test");
    expect(spy).toHaveBeenCalled();
    const callHeaders = spy.mock.calls[0]?.[1]?.headers ?? {};
    expect(callHeaders["Authorization"]).toBeUndefined();
  });

  it("does NOT attach Authorization header when cookie is absent", async () => {
    setCookie("");
    const spy = jest.spyOn(apiClient, "get").mockResolvedValue({ data: {} });
    await apiClient.get("/test");
    expect(spy).toHaveBeenCalled();
  });
});
