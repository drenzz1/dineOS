export const queryKeys = {
  menuItems: {
    all: ["menuItems"] as const,
    list: () => [...queryKeys.menuItems.all, "list"] as const,
  },
  orders: {
    all: ["orders"] as const,
    list: () => [...queryKeys.orders.all, "list"] as const,
  },
} as const;
