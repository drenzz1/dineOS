import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";
import { queryClient } from "@/lib/queryClient";

interface AuthState {
  userId: string | null;
  role: Role | null;
  tenantId: string | null;
  restaurantName: string | null;
  setAuth: (
    userId: string,
    role: Role,
    tenantId: string | null,
    restaurantName?: string | null
  ) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      userId: null,
      role: null,
      tenantId: null,
      restaurantName: null,
      setAuth: (userId, role, tenantId, restaurantName = null) =>
        set({ userId, role, tenantId, restaurantName }),
      clearAuth: () => {
        set({ userId: null, role: null, tenantId: null, restaurantName: null });
        queryClient.clear();
      },
    }),
    { name: "auth" }
  )
);
