import Link from "next/link";
import AdminSidebar from "@/components/admin/AdminSidebar";
import AdminHeader from "@/components/admin/AdminHeader";

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-screen bg-slate-50">
      {/* Sidebar — desktop only */}
      <div className="hidden lg:fixed lg:inset-y-0 lg:flex lg:w-64 lg:flex-col">
        <AdminSidebar />
      </div>

      {/* Content column */}
      <div className="flex flex-1 flex-col lg:pl-64">
        <AdminHeader />

        {/* Mobile nav strip — visible below lg */}
        <nav className="flex gap-1 overflow-x-auto border-b border-slate-200 bg-slate-900 px-4 py-2 lg:hidden">
          {[
            { label: "Dashboard", href: "/admin/dashboard" },
            { label: "Restaurants", href: "/admin/restaurants" },
            { label: "Users", href: "/admin/users" },
          ].map(({ label, href }) => (
            <Link
              key={href}
              href={href}
              className="shrink-0 rounded-md px-3 py-1.5 text-sm font-medium text-slate-300 hover:bg-slate-800 hover:text-white"
            >
              {label}
            </Link>
          ))}
        </nav>

        <main id="main-content" className="flex-1 p-6">{children}</main>
      </div>
    </div>
  );
}
