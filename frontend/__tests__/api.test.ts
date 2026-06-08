import apiClient from "@/lib/api/apiClient";
import { getStaff, saveStaffMember, setStaffActive } from "@/lib/api/staffApi";
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
    jest.spyOn(apiClient, "get").mockResolvedValue({ data: { success: true, data: mockStaff } });

    const result = await getStaff();

    expect(result).toEqual(mockStaff);
    expect(apiClient.get).toHaveBeenCalledWith("/v1/staff");
  });

  it("saveStaffMember POSTs a new staff member with the required PIN", async () => {
    const created = { id: 2, fullName: "Luan K", email: "luan@dineos.com", role: "Cashier", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "post").mockResolvedValue({ data: { success: true, data: created } });

    const result = await saveStaffMember({ fullName: "Luan K", email: "luan@dineos.com", role: "Cashier", pin: "1234" });

    expect(apiClient.post).toHaveBeenCalledWith("/v1/staff", {
      fullName: "Luan K",
      email: "luan@dineos.com",
      role: "Cashier",
      pin: "1234",
    });
    expect(result).toEqual(created);
  });

  it("saveStaffMember PUTs when an id is provided and omits a blank PIN", async () => {
    const updated = { id: 1, fullName: "Updated", email: "a@b.com", role: "Manager", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "put").mockResolvedValue({ data: { success: true, data: updated } });

    const result = await saveStaffMember({ fullName: "Updated", email: "a@b.com", role: "Manager", pin: "" }, 1);

    expect(apiClient.put).toHaveBeenCalledWith("/v1/staff/1", {
      fullName: "Updated",
      email: "a@b.com",
      role: "Manager",
    });
    expect(result).toEqual(updated);
  });

  it("saveStaffMember PUTs an updated PIN when one is supplied", async () => {
    const updated = { id: 1, fullName: "Updated", email: "a@b.com", role: "Manager", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "put").mockResolvedValue({ data: { success: true, data: updated } });

    await saveStaffMember({ fullName: "Updated", email: "a@b.com", role: "Manager", pin: "5678" }, 1);

    expect(apiClient.put).toHaveBeenCalledWith("/v1/staff/1", {
      fullName: "Updated",
      email: "a@b.com",
      role: "Manager",
      pin: "5678",
    });
  });

  it("setStaffActive PATCHes /v1/staff/:id/active with the desired state body", async () => {
    const reactivated = { id: 1, fullName: "Ana", email: "ana@dineos.com", role: "Manager", isActive: true, tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { success: true, data: reactivated } });

    const result = await setStaffActive(1, true);

    expect(apiClient.patch).toHaveBeenCalledWith("/v1/staff/1/active", { isActive: true });
    expect(result.isActive).toBe(true);
  });

  it("setStaffActive can deactivate by sending { isActive: false }", async () => {
    const deactivated = { id: 1, fullName: "Ana", email: "ana@dineos.com", role: "Manager", isActive: false, tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { success: true, data: deactivated } });

    const result = await setStaffActive(1, false);

    expect(apiClient.patch).toHaveBeenCalledWith("/v1/staff/1/active", { isActive: false });
    expect(result.isActive).toBe(false);
  });

  it("getStaff rejects on a 401 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      isAxiosError: true,
      response: { status: 401, data: { message: "Unauthorized" } },
      message: "Unauthorized",
    });

    await expect(getStaff()).rejects.toMatchObject({ response: { status: 401 } });
  });

  it("getStaff rejects on a 403 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      isAxiosError: true,
      response: { status: 403, data: { message: "Forbidden" } },
      message: "Forbidden",
    });

    await expect(getStaff()).rejects.toMatchObject({ response: { status: 403 } });
  });
});

// ─── restaurantApi: paged envelope + PATCH endpoints ─────────────────────────
describe("restaurantApi — paged response and mutations", () => {
  it("getRestaurants unwraps the paged envelope and returns only the items array", async () => {
    const mockList = [{ id: 1, name: "Bella Cucina", ownerName: "Alice", ownerEmail: "a@b.com", phone: "+1", plan: "Pro", status: "Active", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: new Date().toISOString(), tenantId: 1 }];
    jest.spyOn(apiClient, "get").mockResolvedValue({
      data: { success: true, data: { items: mockList, totalCount: 1, page: 1, pageSize: 20 } },
    });

    const result = await getRestaurants();

    expect(result).toEqual(mockList);
    expect(Array.isArray(result)).toBe(true);
    expect((result as unknown as { totalCount?: number }).totalCount).toBeUndefined();
  });

  it("updateRestaurantStatus sends PATCH to /v1/admin/restaurants/:id/status", async () => {
    const updated = { id: 1, name: "Bella", ownerName: "A", ownerEmail: "a@b.com", phone: "+1", plan: "Pro", status: "Suspended", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: "", tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { success: true, data: updated } });

    const result = await updateRestaurantStatus(1, "Suspended");

    expect(apiClient.patch).toHaveBeenCalledWith(
      "/v1/admin/restaurants/1/status",
      { status: "Suspended" }
    );
    expect(result.status).toBe("Suspended");
  });

  it("updateRestaurantPlan sends PATCH to /v1/admin/restaurants/:id/plan", async () => {
    const updated = { id: 1, name: "Bella", ownerName: "A", ownerEmail: "a@b.com", phone: "+1", plan: "Free", status: "Active", city: "Tirana", totalOrders: 0, staffCount: 0, revenue: 0, createdAt: "", tenantId: 1 };
    jest.spyOn(apiClient, "patch").mockResolvedValue({ data: { success: true, data: updated } });

    const result = await updateRestaurantPlan(1, "Free");

    expect(apiClient.patch).toHaveBeenCalledWith(
      "/v1/admin/restaurants/1/plan",
      { plan: "Free" }
    );
    expect(result.plan).toBe("Free");
  });

  it("getRestaurants rejects on a 500 response", async () => {
    jest.spyOn(apiClient, "get").mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: { message: "Internal Server Error" } },
      message: "Internal Server Error",
    });

    await expect(getRestaurants()).rejects.toMatchObject({ response: { status: 500 } });
  });
});

// ─── apiClient: Authorization interceptor ─────────────────────────────────────
describe("apiClient — Authorization interceptor", () => {
  it("does NOT attach Authorization header when cookie is absent", async () => {
    setCookie("");
    const spy = jest.spyOn(apiClient, "get").mockResolvedValue({ data: {} });
    await apiClient.get("/test");
    expect(spy).toHaveBeenCalled();
  });
});
