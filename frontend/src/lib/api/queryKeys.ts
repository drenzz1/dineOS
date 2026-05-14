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
    payments: (tenantId: string | null) =>
      [...queryKeys.orders.all, tenantId, "payments"] as const,
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
  me: {
    all: ["me"] as const,
    current: () => [...queryKeys.me.all, "current"] as const,
  },
  dashboard: {
    manager: (tenantId: string | null) =>
      ["dashboard", tenantId, "manager"] as const,
  },
} as const;
