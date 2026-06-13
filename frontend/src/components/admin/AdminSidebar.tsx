"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ThemeToggle } from "@/components/ui/ThemeToggle";

const NAV_ITEMS = [
  { label: "Dashboard", href: "/admin/dashboard" },
  { label: "Restaurants", href: "/admin/restaurants" },
  { label: "Users", href: "/admin/users" },
];

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export default function AdminSidebar() {
  const pathname = usePathname();

  return (
    <aside className="flex h-full w-64 flex-col bg-surface border-r border-border">
      {/* Logo strip + Platform pill */}
      <div className="flex h-16 shrink-0 items-center gap-2 border-b border-border px-5">
        <div className="flex items-center gap-1.5">
          <span className="inline-flex h-5 w-5 items-center justify-center rounded-sm bg-accent/15">
            <span className="h-2 w-2 rounded-full bg-accent" />
          </span>
          <span className="text-[13px] font-semibold tracking-[-0.01em] text-fg">
            dineOS
          </span>
        </div>
        <span className="ml-auto inline-flex items-center rounded-full bg-surface-2 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.08em] text-fg-muted border border-border">
          Platform
        </span>
      </div>

      {/* Nav */}
      <nav className="flex flex-1 flex-col gap-0.5 p-2.5">
        {NAV_ITEMS.map(({ label, href }) => {
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
        <ThemeToggle />
      </div>
    </aside>
  );
}
