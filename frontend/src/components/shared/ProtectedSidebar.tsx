"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

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

export default function ProtectedSidebar() {
  const pathname = usePathname();

  return (
    <aside className="w-64 border-r border-zinc-200 p-4">
      <nav className="flex flex-col gap-1">
        {navItems.map(({ label, href }) => {
          const isActive =
            pathname === href || pathname.startsWith(href + "/");
          return (
            <Link
              key={href}
              href={href}
              className={
                isActive
                  ? "rounded-md px-3 py-2 text-sm font-medium bg-zinc-100 text-zinc-900"
                  : "rounded-md px-3 py-2 text-sm font-medium text-zinc-500 hover:bg-zinc-50 hover:text-zinc-900 transition-colors"
              }
            >
              {label}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
