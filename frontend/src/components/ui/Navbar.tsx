"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

type Role = "Manager" | "Cashier" | "KitchenStaff";

interface NavLink {
  label: string;
  href: string;
}

interface NavbarProps {
  role: Role;
}

const ALL_LINKS: NavLink[] = [
  { label: "Dashboard", href: "/dashboard" },
  { label: "Orders", href: "/orders" },
  { label: "Payments", href: "/payments" },
  { label: "Kitchen", href: "/kitchen" },
  { label: "Menu", href: "/menu" },
  { label: "Reports", href: "/reports" },
  { label: "Shifts", href: "/shifts" },
  { label: "Staff", href: "/staff" },
];

const ROLE_LINKS: Record<Role, NavLink[]> = {
  Manager: ALL_LINKS,
  Cashier: ALL_LINKS.filter((l) =>
    ["/orders", "/payments", "/kitchen"].includes(l.href)
  ),
  KitchenStaff: ALL_LINKS.filter((l) => l.href === "/kitchen"),
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Navbar({ role }: NavbarProps) {
  const links = ROLE_LINKS[role];
  const pathname = usePathname();

  return (
    <nav aria-label="Main navigation">
      <ul className="flex items-center gap-1">
        {links.map(({ label, href }) => {
          const isActive =
            pathname === href || pathname?.startsWith(`${href}/`);
          return (
            <li key={href}>
              <Link
                href={href}
                aria-current={isActive ? "page" : undefined}
                className={mergeClasses(
                  "inline-flex items-center h-8 px-3 rounded-sm text-[13px] font-medium transition-colors duration-150",
                  isActive
                    ? "bg-accent-soft text-accent"
                    : "text-fg-muted hover:bg-surface-2 hover:text-fg",
                )}
              >
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
