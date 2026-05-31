"use client";

import { useSyncExternalStore } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import { useMe } from "@/hooks/useMe";
import { getPrimaryRole } from "@/lib/auth/keycloak";
import type { Role } from "@/types";

interface NavItem {
  label: string;
  href: string;
}

const navItems: NavItem[] = [
  { label: "Dashboard", href: "/dashboard" },
  { label: "Orders", href: "/orders" },
  { label: "Payments", href: "/payments" },
  { label: "Kitchen", href: "/kitchen" },
  { label: "Menu", href: "/menu" },
  { label: "Reports", href: "/reports" },
  { label: "Shifts", href: "/shifts" },
  { label: "Staff", href: "/staff" },
  { label: "Profile", href: "/settings/profile" },
  { label: "Tables", href: "/settings/tables" },
  { label: "Billing", href: "/settings/billing" },
];

const ROLE_NAV_ITEMS: Record<Exclude<Role, "SuperAdmin">, NavItem[]> = {
  Manager: navItems,
  Cashier: navItems.filter(({ href }) =>
    ["/orders", "/payments", "/kitchen"].includes(href)
  ),
  KitchenStaff: navItems.filter(({ href }) => href === "/kitchen"),
};

// Billing is Manager-only; the filter above already excludes it from Cashier/KitchenStaff.

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

function subscribeClientReady(): () => void {
  return () => {};
}

function getClientSnapshot(): boolean {
  return true;
}

function getServerSnapshot(): boolean {
  return false;
}

export default function ProtectedSidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const { user: me } = useMe();
  const storedRole = useAuthStore((state) => state.role);
  const logout = useAuthStore((state) => state.logout);
  const isStaffSession = useAuthStore((state) => state.isStaffSession);
  const signOutOfShift = useAuthStore((state) => state.signOutOfShift);
  const isClient = useSyncExternalStore(
    subscribeClientReady,
    getClientSnapshot,
    getServerSnapshot
  );
  // In a staff session the active token is the staff-session token, whose role
  // lives in a `role` claim (not realm_access.roles) — so prefer the stored
  // role and never let getPrimaryRole throw the sidebar down.
  const meRole = (() => {
    if (!me) return null;
    try {
      return getPrimaryRole(me.roles);
    } catch {
      return null;
    }
  })();
  const role = !isClient ? null : isStaffSession ? storedRole : meRole ?? storedRole;

  const baseNavItems =
    role && role !== "SuperAdmin" ? ROLE_NAV_ITEMS[role] : [];
  // Account-level screens (Staff, Billing) are Owner-only — hide them in an
  // operational staff session so a PIN-selected Manager doesn't hit a raw 403.
  const visibleNavItems = isStaffSession
    ? baseNavItems.filter(
        ({ href }) => href !== "/staff" && href !== "/settings/billing"
      )
    : baseNavItems;

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  const handleSwitchUser = async () => {
    await signOutOfShift();
    router.push("/select-staff");
  };

  return (
    <aside className="hidden md:flex md:w-64 md:shrink-0 md:flex-col bg-surface border-r border-border">
      {/* Brand strip */}
      <div className="flex h-14 items-center gap-2 border-b border-border px-5">
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-sm bg-accent/15">
          <span className="h-2 w-2 rounded-full bg-accent" />
        </span>
        <span className="text-[13px] font-semibold tracking-[-0.01em] text-fg">
          dineOS
        </span>
      </div>

      <nav aria-label="Main" className="flex flex-col gap-0.5 p-2.5">
        {visibleNavItems.map(({ label, href }) => {
          const isActive =
            pathname === href || pathname?.startsWith(href + "/");
          return (
            <Link
              key={href}
              href={href}
              aria-current={isActive ? "page" : undefined}
              className={mergeClasses(
                "flex items-center h-8 rounded-sm px-3 text-[13px] font-medium transition-colors duration-150",
                isActive
                  ? "bg-accent-soft text-accent"
                  : "text-fg-muted hover:bg-surface-2 hover:text-fg",
              )}
            >
              {label}
            </Link>
          );
        })}
      </nav>

      <div className="mt-auto border-t border-border p-2.5">
        {role && role !== "SuperAdmin" && (
          <button
            onClick={handleSwitchUser}
            className="flex items-center h-8 rounded-sm px-3 text-[13px] font-medium text-fg-muted hover:bg-surface-2 hover:text-fg transition-colors duration-150 w-full text-left"
          >
            Switch user
          </button>
        )}
        <button
          onClick={handleLogout}
          className="flex items-center h-8 rounded-sm px-3 text-[13px] font-medium text-fg-muted hover:bg-surface-2 hover:text-fg transition-colors duration-150 w-full text-left"
        >
          Log out
        </button>
      </div>
    </aside>
  );
}
