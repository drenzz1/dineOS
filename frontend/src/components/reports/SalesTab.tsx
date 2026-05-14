"use client";

import { useSalesReport } from "@/hooks/useReports";
import { Stat } from "@/components/ui/Stat";
import { DataTable } from "@/components/ui/DataTable";
import { Skeleton } from "@/components/ui/Skeleton";

interface SalesTabProps {
  from: string;
  to: string;
}

export default function SalesTab({ from, to }: SalesTabProps) {
  const { report, isLoading, isError } = useSalesReport(from, to);

  if (isError) {
    return (
      <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
        <p className="text-[13px] text-status-cancelled-fg">
          Failed to load sales report. Please refresh.
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

  const methodRows = report.byPaymentMethod.map((item) => ({
    method: item.method,
    total: `€${item.total.toFixed(2)}`,
    count: String(item.count),
  })) as unknown as Array<Record<string, unknown>>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Stat label="Total Orders" value={report.orderCount} />
        <Stat label="Total Revenue" value={`€${report.totalRevenue.toFixed(2)}`} />
        <Stat label="Average Ticket" value={`€${report.averageTicket.toFixed(2)}`} />
      </div>
      <div>
        <h2 className="text-[13px] font-semibold text-fg mb-3">By Payment Method</h2>
        <DataTable
          columns={[
            { key: "method", header: "Method" },
            { key: "total", header: "Total (€)" },
            { key: "count", header: "Orders" },
          ]}
          data={methodRows}
          emptyMessage="No payment data for this period."
        />
      </div>
    </div>
  );
}
