"use client";

import { useOrdersReport } from "@/hooks/useReports";
import { Stat } from "@/components/ui/Stat";
import { DataTable } from "@/components/ui/DataTable";
import { Skeleton } from "@/components/ui/Skeleton";

interface OrdersTabProps {
  from: string;
  to: string;
}

export default function OrdersTab({ from, to }: OrdersTabProps) {
  const { report, isLoading, isError } = useOrdersReport(from, to);

  if (isError) {
    return (
      <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
        <p className="text-[13px] text-status-cancelled-fg">
          Failed to load orders report. Please refresh.
        </p>
      </div>
    );
  }

  if (isLoading || !report) {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-1">
          <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-3">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-7 w-32" />
          </div>
        </div>
        <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
        <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
      </div>
    );
  }

  const statusRows = report.byStatus.map((item) => ({
    status: item.status,
    count: String(item.count),
  })) as unknown as Array<Record<string, unknown>>;

  const typeRows = report.byType.map((item) => ({
    orderType: item.orderType,
    count: String(item.count),
  })) as unknown as Array<Record<string, unknown>>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Stat label="Total Orders" value={report.totalOrders} />
      </div>
      <div>
        <h2 className="text-[13px] font-semibold text-fg mb-3">By Status</h2>
        <DataTable
          columns={[
            { key: "status", header: "Status" },
            { key: "count", header: "Orders" },
          ]}
          data={statusRows}
          emptyMessage="No order status data for this period."
        />
      </div>
      <div>
        <h2 className="text-[13px] font-semibold text-fg mb-3">By Order Type</h2>
        <DataTable
          columns={[
            { key: "orderType", header: "Order Type" },
            { key: "count", header: "Orders" },
          ]}
          data={typeRows}
          emptyMessage="No order type data for this period."
        />
      </div>
    </div>
  );
}
