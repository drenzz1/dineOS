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
  persistBusinessToken,
  persistStaffSessionCookies,
  persistStaffRefreshToken,
  persistRoleCookie,
  getBusinessToken,
  getStaffRefreshToken,
  clearStaffRefreshToken,
  clearAuthCookies,
  getDestination,
} from "@/lib/auth/keycloak";
import { getMe } from "@/lib/api/meApi";
import { getRestaurantProfile } from "@/lib/api/restaurantProfileApi";
import {
  startStaffSession as apiStartStaffSession,
  endStaffSession as apiEndStaffSession,
} from "@/lib/api/staffSessionApi";

// The Keycloak business login resolves to Manager (owners are Owner→Manager
// composites); the operational role then comes from the PIN-selected staff
// session. Used to restore "owner mode" when a staff session ends.
const OWNER_MODE_ROLE = "Manager" as const;

interface AuthState {
  userId: string | null;
  role: Role | "SuperAdmin" | null;
  tenantId: string | null;
  restaurantName: string | null;
  accessToken: string | null;
  // True once a PIN-selected staff session is active (operational mode). In
  // this mode the active token is the staff-session token, which lacks the
  // account-level Owner role — so staff/billing screens are hidden.
  isStaffSession: boolean;
  activeStaffName: string | null;
  login: (username: string, password: string, from?: string | null) => Promise<{ destination: string }>;
  startStaffSession: (staffMemberId: number, pin: string) => Promise<{ role: Role }>;
  endStaffSession: () => void;
  signOutOfShift: () => Promise<void>;
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
      isStaffSession: false,
      activeStaffName: null,
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

          // Retain the business (Keycloak/Owner) token so the staff roster can
          // start a PIN session and "switch user" can restore owner mode.
          persistBusinessToken(tokens.accessToken, tokens.expiresIn);

          set({
            userId: me.id,
            role,
            tenantId: me.tenantId,
            restaurantName,
            accessToken: tokens.accessToken,
            isStaffSession: false,
            activeStaffName: null,
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
      startStaffSession: async (staffMemberId, pin) => {
        // Verifies the PIN against the business (Keycloak) token and returns a
        // role-scoped staff-session token. We swap it into the active
        // access_token so all operational API calls now carry the staff role,
        // while business_token (owner) is left intact for "switch user".
        const session = await apiStartStaffSession(staffMemberId, pin);
        const { tenantId } = get();

        persistStaffSessionCookies(
          session.accessToken,
          session.role,
          session.expiresIn,
          tenantId
        );
        // Retain the refresh token so the apiClient can renew the access token
        // mid-shift without a re-PIN.
        persistStaffRefreshToken(session.refreshToken, session.refreshExpiresIn);

        set({
          role: session.role,
          accessToken: session.accessToken,
          isStaffSession: true,
          activeStaffName: session.fullName,
        });

        return { role: session.role };
      },
      endStaffSession: () => {
        // Restore owner mode: the business (Keycloak) token becomes the active
        // token again so account screens (staff/billing) work. Caller routes
        // back to the roster. Local-only (no network) — used by the apiClient
        // when a refresh has already failed; see signOutOfShift for the
        // revoking variant.
        clearStaffRefreshToken();
        const businessToken = getBusinessToken();
        if (businessToken) {
          persistAccessTokenCookie(businessToken);
          persistRoleCookie(OWNER_MODE_ROLE);
        }
        set({
          role: businessToken ? OWNER_MODE_ROLE : get().role,
          accessToken: businessToken ?? get().accessToken,
          isStaffSession: false,
          activeStaffName: null,
        });
      },
      signOutOfShift: async () => {
        // "Switch user": revoke the staff tokens server-side (best-effort) so
        // they can't be reused, then restore owner mode locally.
        const accessToken = get().accessToken;
        const refreshToken = getStaffRefreshToken();
        if (get().isStaffSession && accessToken && refreshToken) {
          try {
            await apiEndStaffSession(accessToken, refreshToken);
          } catch {
            // Revocation is best-effort; the tokens are short-lived regardless.
          }
        }
        get().endStaffSession();
      },
      logout: async () => {
        try {
          await apiLogout();
        } catch {
          // Backend call may fail — always clean up regardless
        }

        clearAuthCookies();
        set({
          userId: null,
          role: null,
          tenantId: null,
          restaurantName: null,
          accessToken: null,
          isStaffSession: false,
          activeStaffName: null,
        });
        queryClient.removeQueries({ queryKey: queryKeys.me.all });
        queryClient.clear();
      },
      setAuth: (userId, role, tenantId, restaurantName = null, accessToken = null) =>
        set({ userId, role, tenantId, restaurantName, accessToken }),
      clearAuth: () => {
        set({
          userId: null,
          role: null,
          tenantId: null,
          restaurantName: null,
          accessToken: null,
          isStaffSession: false,
          activeStaffName: null,
        });
        queryClient.removeQueries({ queryKey: queryKeys.me.all });
        queryClient.clear();
      },
    }),
    { name: "auth" }
  )
);
