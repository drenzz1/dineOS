"use client";

import { useStaffReport } from "@/hooks/useReports";
import { Stat } from "@/components/ui/Stat";
import { DataTable } from "@/components/ui/DataTable";
import { Skeleton } from "@/components/ui/Skeleton";

export default function StaffTab() {
  const { report, isLoading, isError } = useStaffReport();

  if (isError) {
    return (
      <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
        <p className="text-[13px] text-status-cancelled-fg">
          Failed to load staff report. Please refresh.
        </p>
      </div>
    );
  }

  if (isLoading || !report) {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-3">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-7 w-32" />
            </div>
          ))}
        </div>
        <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
      </div>
    );
  }

  const roleRows = report.byRole.map((item) => ({
    role: item.role,
    total: String(item.total),
    active: String(item.active),
    inactive: String(item.total - item.active),
  })) as unknown as Array<Record<string, unknown>>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Stat label="Total Staff" value={report.total} />
        <Stat label="Active" value={report.active} />
        <Stat label="Inactive" value={report.inactive} />
      </div>
      <div>
        <h2 className="text-[13px] font-semibold text-fg mb-3">By Role</h2>
        <DataTable
          columns={[
            { key: "role", header: "Role" },
            { key: "total", header: "Total" },
            { key: "active", header: "Active" },
            { key: "inactive", header: "Inactive" },
          ]}
          data={roleRows}
          emptyMessage="No staff role data available."
        />
      </div>
    </div>
  );
}
