import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";
import { queryClient } from "@/lib/queryClient";

interface AuthState {
  userId: string | null;
  role: Role | "SuperAdmin" | null;
  tenantId: string | null;
  restaurantName: string | null;
  accessToken: string | null;
  setAuth: (
    userId: string,
    role: Role | "SuperAdmin",
    tenantId: string | null,
    restaurantName?: string | null,
    accessToken?: string | null
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
      accessToken: null,
      setAuth: (userId, role, tenantId, restaurantName = null, accessToken = null) =>
        set({ userId, role, tenantId, restaurantName, accessToken }),
      clearAuth: () => {
        set({ userId: null, role: null, tenantId: null, restaurantName: null, accessToken: null });
        queryClient.clear();
      },
    }),
    { name: "auth" }
  )
);
