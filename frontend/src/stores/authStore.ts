import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Role } from "@/types";
import { queryClient } from "@/lib/queryClient";
import { queryKeys } from "@/lib/api/queryKeys";
import { login as apiLogin, logout as apiLogout } from "@/lib/auth/authApi";
import {
  getPrimaryRole,
  persistAccessTokenCookie,
  persistAuthCookies,
  clearAuthCookies,
  getDestination,
} from "@/lib/auth/keycloak";
import { getMe } from "@/lib/api/meApi";
import { getRestaurantProfile } from "@/lib/api/restaurantProfileApi";

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
    (set, get) => ({
      userId: null,
      role: null,
      tenantId: null,
      restaurantName: null,
      accessToken: null,
      login: async (username, password, from = null) => {
        // Snapshot current state so a failed re-login from an already
        // authenticated session does not silently log the user out.
        const prev = get();
        const snapshot = {
          userId: prev.userId,
          role: prev.role,
          tenantId: prev.tenantId,
          restaurantName: prev.restaurantName,
          accessToken: prev.accessToken,
        };

        try {
          const tokens = await apiLogin(username, password);

          // Persist the access token BEFORE the /me + profile calls below.
          // The apiClient request interceptor authorizes requests from the
          // access_token cookie; on a first-time login (no pre-existing
          // cookie) /me would otherwise be sent with no bearer token → 401 →
          // interceptor refresh fails → bounce back to /login. Role and tenant
          // cookies are written once known via persistAuthCookies further down.
          persistAccessTokenCookie(tokens.accessToken, tokens.expiresIn);

          const me = await getMe();
          const role = getPrimaryRole(me.roles);

          // restaurantName is always non-null after a successful login:
          //  - SuperAdmin → "Platform" sentinel (no tenant exists).
          //  - Tenant roles → real profile name, or "My Restaurant"
          //    placeholder if the profile call fails.
          let restaurantName: string;
          if (me.tenantId) {
            try {
              const profile = await getRestaurantProfile();
              restaurantName = profile.name ?? "My Restaurant";
            } catch {
              restaurantName = "My Restaurant";
            }
          } else {
            restaurantName = "Platform";
          }

          persistAuthCookies(
            tokens.accessToken,
            tokens.refreshToken,
            tokens.expiresIn,
            tokens.refreshExpiresIn,
            role,
            me.tenantId
          );

          set({
            userId: me.id,
            role,
            tenantId: me.tenantId,
            restaurantName,
            accessToken: tokens.accessToken,
          });

          return { destination: getDestination(role, from) };
        } catch (err) {
          // Restore prior session on failure — never silently log out
          // an already-authenticated user because of a transient error.
          // We wrote the new access_token cookie above, so undo it: clear
          // everything for a fresh login, or restore the prior token cookie
          // when re-logging in from an existing session.
          if (snapshot.accessToken === null) {
            clearAuthCookies();
          } else {
            persistAccessTokenCookie(snapshot.accessToken);
          }
          set(snapshot);
          throw err;
        }
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
