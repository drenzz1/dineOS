import { Button } from "@/components/ui/Button";
import RoleBadge from "./RoleBadge";
import type { StaffMember } from "@/types/staff";

// ─── Status badge ─────────────────────────────────────────────────────────────

function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
        isActive
          ? "bg-green-100 text-green-800"
          : "bg-zinc-100 text-zinc-500"
      }`}
    >
      {isActive ? "Active" : "Inactive"}
    </span>
  );
}

// ─── Skeleton ─────────────────────────────────────────────────────────────────

export function StaffTableSkeleton() {
  return (
    <div className="animate-pulse space-y-2">
      <div className="h-10 rounded-lg bg-zinc-100" />
      {[0, 1, 2, 3, 4].map((i) => (
        <div key={i} className="h-16 rounded-lg bg-zinc-100" />
      ))}
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
      <div className="flex items-center justify-center rounded-lg border border-dashed border-zinc-300 py-16">
        <p className="text-sm text-zinc-400">No staff members found.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200">
      <table className="w-full min-w-[640px] text-sm">
        <thead className="border-b border-zinc-200 bg-zinc-50">
          <tr>
            {["Name", "Email", "Role", "Status", "Actions"].map((col) => (
              <th
                key={col}
                className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 bg-white">
          {staff.map((member) => (
            <tr
              key={member.id}
              className={`transition-colors hover:bg-zinc-50 ${
                member.isActive ? "" : "opacity-50"
              }`}
            >
              {/* Name */}
              <td className="px-4 py-3 font-medium text-zinc-900">
                {member.fullName}
              </td>

              {/* Email */}
              <td className="px-4 py-3 text-zinc-600">{member.email}</td>

              {/* Role */}
              <td className="px-4 py-3">
                <RoleBadge role={member.role} />
              </td>

              {/* Status */}
              <td className="px-4 py-3">
                <StatusBadge isActive={member.isActive} />
              </td>

              {/* Actions */}
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
