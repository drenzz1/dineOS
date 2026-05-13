import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";
import { queryClient } from "@/lib/queryClient";
import { login as apiLogin } from "@/lib/auth/authApi";
import {
  decodeAccessTokenClaims,
  persistAuthCookies,
  getDestination,
} from "@/lib/auth/keycloak";

interface AuthState {
  userId: string | null;
  role: Role | "SuperAdmin" | null;
  tenantId: string | null;
  restaurantName: string | null;
  accessToken: string | null;
  login: (username: string, password: string, from?: string | null) => Promise<{ destination: string }>;
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
        const { userId, role, tenantId } = decodeAccessTokenClaims(tokens.accessToken);

        persistAuthCookies(
          tokens.accessToken,
          tokens.refreshToken,
          tokens.expiresIn,
          tokens.refreshExpiresIn,
          role,
          tenantId
        );

        set({
          userId,
          role,
          tenantId,
          restaurantName: tenantId ? "Olio & Sale" : null,
          accessToken: tokens.accessToken,
        });

        return { destination: getDestination(role, from) };
      },
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
