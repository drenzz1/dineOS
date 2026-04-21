import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";

interface AuthState {
  userId: string | null;
  role: Role | null;
  tenantId: string | null;
  setAuth: (userId: string, role: Role, tenantId: string | null) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      userId: null,
      role: null,
      tenantId: null,
      setAuth: (userId, role, tenantId) => set({ userId, role, tenantId }),
      clearAuth: () => set({ userId: null, role: null, tenantId: null }),
    }),
    { name: "auth" }
  )
);
