"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuthStore } from "@/stores/authStore";
import type { Role } from "@/types";

interface NavItem {
  label: string;
  href: string;
}

const navItems: NavItem[] = [
  { label: "Dashboard", href: "/dashboard" },
  { label: "Orders", href: "/orders" },
  { label: "Kitchen", href: "/kitchen" },
  { label: "Menu", href: "/menu" },
  { label: "Reports", href: "/reports" },
  { label: "Shifts", href: "/shifts" },
  { label: "Staff", href: "/staff" },
];

const ROLE_NAV_ITEMS: Record<Exclude<Role, "SuperAdmin">, NavItem[]> = {
  Manager: navItems,
  Cashier: navItems.filter(({ href }) => ["/orders", "/kitchen"].includes(href)),
  KitchenStaff: navItems.filter(({ href }) => href === "/kitchen"),
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

function getCookieRole(): Role | null {
  if (typeof document === "undefined") return null;

  const roleCookie = document.cookie
    .split("; ")
    .find((cookie) => cookie.startsWith("role="));
  const role = roleCookie?.split("=")[1];

  if (
    role === "Manager" ||
    role === "Cashier" ||
    role === "KitchenStaff" ||
    role === "SuperAdmin"
  ) {
    return role;
  }

  return null;
}

export default function ProtectedSidebar() {
  const pathname = usePathname();
  const storedRole = useAuthStore((state) => state.role);
  const role = storedRole ?? getCookieRole();
  const visibleNavItems =
    role && role !== "SuperAdmin" ? ROLE_NAV_ITEMS[role] : [];

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
    </aside>
  );
}
