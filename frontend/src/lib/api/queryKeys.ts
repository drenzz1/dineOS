export const queryKeys = {
  menuItems: {
    all: ["menuItems"] as const,
    list: () => [...queryKeys.menuItems.all, "list"] as const,
  },
  menu: {
    all: ["menu"] as const,
    list: () => [...queryKeys.menu.all, "list"] as const,
  },
  orders: {
    all: ["orders"] as const,
    list: () => [...queryKeys.orders.all, "list"] as const,
  },
  staff: {
    all: ["staff"] as const,
    list: () => [...queryKeys.staff.all, "list"] as const,
  },
  shifts: {
    all: ["shifts"] as const,
    list: () => [...queryKeys.shifts.all, "list"] as const,
  },
} as const;
