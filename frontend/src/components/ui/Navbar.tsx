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
  { label: "Kitchen", href: "/kitchen" },
  { label: "Menu", href: "/menu" },
  { label: "Reports", href: "/reports" },
  { label: "Shifts", href: "/shifts" },
  { label: "Staff", href: "/staff" },
];

const ROLE_LINKS: Record<Role, NavLink[]> = {
  Manager: ALL_LINKS,
  Cashier: ALL_LINKS.filter((l) => ["/orders", "/kitchen"].includes(l.href)),
  KitchenStaff: ALL_LINKS.filter((l) => l.href === "/kitchen"),
};

export function Navbar({ role }: NavbarProps) {
  const links = ROLE_LINKS[role];

  return (
    <nav aria-label="Main navigation">
      <ul className="flex items-center gap-4">
        {links.map(({ label, href }) => (
          <li key={href}>
            <a
              href={href}
              className="text-sm font-medium text-zinc-600 hover:text-zinc-900 transition-colors"
            >
              {label}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}
