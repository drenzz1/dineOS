import type { Role } from "@/types/staff";

interface RoleBadgeProps {
  role: Role;
  className?: string;
}

const ROLE_CLASSES: Record<Role, string> = {
  Manager: "bg-purple-100 text-purple-800",
  Cashier: "bg-blue-100 text-blue-800",
  KitchenStaff: "bg-green-100 text-green-800",
  SuperAdmin: "bg-zinc-100 text-zinc-600",
};

const ROLE_LABELS: Record<Role, string> = {
  Manager: "Manager",
  Cashier: "Cashier",
  KitchenStaff: "Kitchen Staff",
  SuperAdmin: "Super Admin",
};

export default function RoleBadge({ role, className = "" }: RoleBadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${ROLE_CLASSES[role]} ${className}`}
    >
      {ROLE_LABELS[role]}
    </span>
  );
}
