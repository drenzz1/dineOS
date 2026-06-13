import Link from "next/link";
import AdminSidebar from "@/components/admin/AdminSidebar";
import AdminHeader from "@/components/admin/AdminHeader";

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div data-chrome="admin" className="flex min-h-screen bg-bg text-fg">
      {/* Sidebar — desktop only */}
      <div className="hidden lg:fixed lg:inset-y-0 lg:flex lg:w-64 lg:flex-col">
        <AdminSidebar />
      </div>

      {/* Content column */}
      <div className="flex flex-1 flex-col lg:pl-64">
        <AdminHeader />

        {/* Mobile nav strip — visible below lg */}
        <nav
          aria-label="Platform sections"
          className="flex gap-1 overflow-x-auto border-b border-border bg-surface px-4 py-2 lg:hidden"
        >
          {[
            { label: "Dashboard", href: "/admin/dashboard" },
            { label: "Restaurants", href: "/admin/restaurants" },
            { label: "Users", href: "/admin/users" },
            { label: "Settings", href: "/admin/settings" },
          ].map(({ label, href }) => (
            <Link
              key={href}
              href={href}
              className="shrink-0 rounded-sm px-3 py-1.5 text-[13px] font-medium text-fg-muted hover:bg-surface-2 hover:text-fg transition-colors"
            >
              {label}
            </Link>
          ))}
        </nav>

        <main
          id="main-content"
          className="flex-1 p-6 animate-fade-up"
        >
          {children}
        </main>
      </div>
    </div>
  );
}
