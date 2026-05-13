import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";
import { queryClient } from "@/lib/queryClient";
import { queryKeys } from "@/lib/api/queryKeys";
import { login as apiLogin, logout as apiLogout } from "@/lib/auth/authApi";
import {
  getPrimaryRole,
  persistAuthCookies,
  clearAuthCookies,
  getDestination,
} from "@/lib/auth/keycloak";
import { getMe } from "@/lib/api/meApi";

interface AuthState {
  userId: string | null;
  role: Role | "SuperAdmin" | null;
  tenantId: string | null;
  restaurantName: string | null;
  accessToken: string | null;
  login: (username: string, password: string, from?: string | null) => Promise<{ destination: string }>;
  logout: () => Promise<void>;
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
      login: async (username, password, from = null) => {
        const tokens = await apiLogin(username, password);

        persistAuthCookies(
          tokens.accessToken,
          tokens.refreshToken,
          tokens.expiresIn,
          tokens.refreshExpiresIn,
          "Manager",
          null
        );

        const me = await getMe();
        const role = getPrimaryRole(me.roles);

        persistAuthCookies(
          tokens.accessToken,
          tokens.refreshToken,
          tokens.expiresIn,
          tokens.refreshExpiresIn,
          role,
          me.tenantId
        );

        const restaurantName = me.tenantId ? "Olio & Sale" : null;

        set({
          userId: me.id,
          role,
          tenantId: me.tenantId,
          restaurantName,
          accessToken: tokens.accessToken,
        });

        return { destination: getDestination(role, from) };
      },
      logout: async () => {
        try {
          await apiLogout();
        } catch {
          // Backend call may fail — always clean up regardless
        }

        clearAuthCookies();
        set({ userId: null, role: null, tenantId: null, restaurantName: null, accessToken: null });
        queryClient.removeQueries({ queryKey: queryKeys.me.all });
        queryClient.clear();
      },
      setAuth: (userId, role, tenantId, restaurantName = null, accessToken = null) =>
        set({ userId, role, tenantId, restaurantName, accessToken }),
      clearAuth: () => {
        set({ userId: null, role: null, tenantId: null, restaurantName: null, accessToken: null });
        queryClient.removeQueries({ queryKey: queryKeys.me.all });
        queryClient.clear();
      },
    }),
    { name: "auth" }
  )
);
