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
    list: (tenantId: string | null) =>
      [...queryKeys.orders.all, tenantId, "list"] as const,
    byDate: (tenantId: string | null, date: string) =>
      [...queryKeys.orders.all, tenantId, { date }] as const,
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
    list: (tenantId: string | null) =>
      [...queryKeys.shifts.all, tenantId, "list"] as const,
  },
  adminUsers: {
    all: ["adminUsers"] as const,
    list: () => [...queryKeys.adminUsers.all, "list"] as const,
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
  dashboard: {
    manager: (tenantId: string | null) =>
      ["dashboard", tenantId, "manager"] as const,
  },
} as const;
