export const queryKeys = {
  menuItems: {
    all: ["menuItems"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.menuItems.all, tenantId, "list"] as const,
  },
  menu: {
    all: ["menu"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.menu.all, tenantId, "list"] as const,
  },
  menuCategories: {
    all: ["menuCategories"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.menuCategories.all, tenantId, "list"] as const,
  },
  orders: {
    all: ["orders"] as const,
    list: (
      tenantId: string | null,
      filters?: { date?: string; status?: string }
    ) =>
      [
        ...queryKeys.orders.all,
        tenantId,
        "list",
        filters?.date ?? null,
        filters?.status ?? "all",
      ] as const,
    byDate: (tenantId: string | null, date: string) =>
      [...queryKeys.orders.all, tenantId, { date }] as const,
    detail: (id: string) => [...queryKeys.orders.all, "detail", id] as const,
    kitchen: (tenantId: string | null) =>
      [
        ...queryKeys.orders.all,
        tenantId,
        { status: ["New", "InProgress"] },
      ] as const,
    kitchenQueue: (tenantId: string | null) =>
      [...queryKeys.orders.all, tenantId, "kitchenQueue"] as const,
  },
  payments: {
    all: ["payments"] as const,
    openOrders: (tenantId: string | null) =>
      [...queryKeys.payments.all, tenantId, "openOrders"] as const,
  },
  billing: {
    all: ["billing"] as const,
    subscription: (tenantId: string | null) =>
      [...queryKeys.billing.all, tenantId, "subscription"] as const,
    invoices: (tenantId: string | null) =>
      [...queryKeys.billing.all, tenantId, "invoices"] as const,
  },
  staff: {
    all: ["staff"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.staff.all, tenantId, "list"] as const,
  },
  shifts: {
    all: ["shifts"] as const,
    list: (tenantId: string | null, date?: string) =>
      date
        ? ([...queryKeys.shifts.all, tenantId, "list", date] as const)
        : ([...queryKeys.shifts.all, tenantId, "list"] as const),
  },
  shiftNotes: {
    all: ["shiftNotes"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.shiftNotes.all, tenantId, "list"] as const,
  },
  adminUsers: {
    all: ["adminUsers"] as const,
    list: (params?: { search?: string; page?: number; pageSize?: number }) =>
      [
        ...queryKeys.adminUsers.all,
        "list",
        params?.search ?? null,
        params?.page ?? 1,
        params?.pageSize ?? 20,
      ] as const,
  },
  adminRestaurants: {
    all: ["adminRestaurants"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.adminRestaurants.all, tenantId, "list"] as const,
    detail: (id: number) => [...queryKeys.adminRestaurants.all, id] as const,
  },
  adminAnalytics: {
    all: ["admin", "analytics"] as const,
  },
  reports: {
    all: ["reports"] as const,
    sales: (tenantId: string | null, from: string, to: string) =>
      [...queryKeys.reports.all, tenantId, "sales", from, to] as const,
    orders: (tenantId: string | null, from: string, to: string) =>
      [...queryKeys.reports.all, tenantId, "orders", from, to] as const,
    staff: (tenantId: string | null) =>
      [...queryKeys.reports.all, tenantId, "staff"] as const,
  },
  me: {
    all: ["me"] as const,
    current: () => [...queryKeys.me.all, "current"] as const,
  },
  dashboard: {
    manager: (tenantId: string | null) =>
      ["dashboard", tenantId, "manager"] as const,
  },
  restaurantProfile: {
    all: ["restaurantProfile"] as const,
    current: (tenantId: string | null) =>
      [...queryKeys.restaurantProfile.all, tenantId] as const,
  },
  restaurantTables: {
    all: ["restaurantTables"] as const,
    list: (tenantId: string | null) =>
      [...queryKeys.restaurantTables.all, tenantId, "list"] as const,
  },
  signup: {
    all: ["signup"] as const,
    status: (sessionId: string) =>
      [...queryKeys.signup.all, "status", sessionId] as const,
  },
  demo: {
    all: ["demo"] as const,
    request: () => [...queryKeys.demo.all, "request"] as const,
  },
} as const;
