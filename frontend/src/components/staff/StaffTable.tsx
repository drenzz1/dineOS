import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";
import { Skeleton } from "@/components/ui/Skeleton";
import RoleBadge from "./RoleBadge";
import type { StaffMember } from "@/types/staff";

// ─── Status badge ─────────────────────────────────────────────────────────────

function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-semibold border ${
        isActive
          ? "bg-status-ready-bg text-status-ready-fg border-status-ready-border"
          : "bg-status-delivered-bg text-status-delivered-fg border-status-delivered-border"
      }`}
    >
      <span
        aria-hidden="true"
        className={`h-1.5 w-1.5 rounded-full ${
          isActive ? "bg-status-ready-solid" : "bg-status-delivered-solid"
        }`}
      />
      {isActive ? "Active" : "Inactive"}
    </span>
  );
}

// ─── Skeleton ─────────────────────────────────────────────────────────────────

export function StaffTableSkeleton() {
  return (
    <div className="rounded-md border border-border bg-surface shadow-sm">
      <div className="border-b border-border bg-surface-2 px-4 py-3">
        <Skeleton className="h-3 w-24" />
      </div>
      <div className="divide-y divide-border">
        {[0, 1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className="grid grid-cols-[1.4fr_1.6fr_0.8fr_0.8fr_1fr] items-center gap-4 px-4 py-3"
          >
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-3 w-36" />
            <Skeleton className="h-5 w-16 rounded-full" />
            <Skeleton className="h-5 w-16 rounded-full" />
            <Skeleton className="h-7 w-20 rounded-sm" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Table ────────────────────────────────────────────────────────────────────

interface StaffTableProps {
  staff: StaffMember[];
  onEdit: (member: StaffMember) => void;
  onToggleActive: (member: StaffMember) => void;
}

export function StaffTable({ staff, onEdit, onToggleActive }: StaffTableProps) {
  if (staff.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-border-strong bg-surface">
        <EmptyState
          illustration={<Illo.Staff />}
          title="No staff members yet"
          description="Add your first team member to start assigning roles and tracking shifts."
        />
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-md border border-border bg-surface shadow-sm">
      <table className="w-full min-w-[640px] text-[13px]">
        <thead className="border-b border-border bg-surface-2">
          <tr>
            {["Name", "Email", "Role", "Status", "Actions"].map((col) => (
              <th
                key={col}
                className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {staff.map((member) => (
            <tr
              key={member.id}
              className={`transition-colors duration-150 hover:bg-surface-2 ${
                member.isActive ? "" : "opacity-70"
              }`}
            >
              <td className="px-4 py-3 font-medium text-fg">
                {member.fullName}
              </td>
              <td className="px-4 py-3 text-fg-muted">{member.email}</td>
              <td className="px-4 py-3">
                <RoleBadge role={member.role} />
              </td>
              <td className="px-4 py-3">
                <StatusBadge isActive={member.isActive} />
              </td>
              <td className="px-4 py-3">
                <div className="flex items-center gap-2">
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => onEdit(member)}
                  >
                    Edit
                  </Button>
                  <Button
                    size="sm"
                    variant={member.isActive ? "danger" : "ghost"}
                    onClick={() => onToggleActive(member)}
                  >
                    {member.isActive ? "Deactivate" : "Reactivate"}
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
