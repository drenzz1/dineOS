import type { Role } from "@/types/staff";

interface RoleBadgeProps {
  role: Role;
  className?: string;
}

const ROLE_CLASSES: Record<Role, string> = {
  Manager: "bg-accent-soft text-accent-hover border-ember-200",
  Cashier: "bg-status-new-bg text-status-new-fg border-status-new-border",
  KitchenStaff: "bg-status-ready-bg text-status-ready-fg border-status-ready-border",
  SuperAdmin: "bg-surface-2 text-fg-muted border-border-strong",
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
      className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-semibold ${ROLE_CLASSES[role]} ${className}`}
    >
      {ROLE_LABELS[role]}
    </span>
  );
}
