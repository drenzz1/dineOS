"use client";

import { useItemsReport } from "@/hooks/useReports";
import { Skeleton } from "@/components/ui/Skeleton";

interface ItemsTabProps {
  from: string;
  to: string;
}

export default function ItemsTab({ from, to }: ItemsTabProps) {
  const { report, isLoading, isError } = useItemsReport(from, to);

  if (isError) {
    return (
      <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
        <p className="text-[13px] text-status-cancelled-fg">
          Failed to load items report. Please refresh.
        </p>
      </div>
    );
  }

  if (isLoading || !report) {
    return (
      <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
        {[0, 1, 2, 3, 4].map((i) => (
          <Skeleton key={i} className="h-4 w-full" />
        ))}
      </div>
    );
  }

  if (report.topItems.length === 0) {
    return (
      <div className="bg-surface border border-border rounded-md shadow-sm px-4 py-8 text-center">
        <p className="text-[13px] text-fg-muted">No order item data for this period.</p>
      </div>
    );
  }

  const maxQty = Math.max(...report.topItems.map((i) => i.quantity), 1);

  return (
    <div className="space-y-4">
      <div className="bg-surface border border-border rounded-md shadow-sm overflow-hidden">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="border-b border-border bg-surface-2">
              <th className="px-4 py-3 text-left font-semibold text-fg-muted w-8">#</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Item</th>
              <th className="px-4 py-3 text-right font-semibold text-fg-muted whitespace-nowrap">Qty Sold</th>
              <th className="px-4 py-3 text-right font-semibold text-fg-muted whitespace-nowrap">Revenue</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted w-40 hidden sm:table-cell">
                Popularity
              </th>
            </tr>
          </thead>
          <tbody>
            {report.topItems.map((item, idx) => (
              <tr key={item.name} className="border-b border-border last:border-0 hover:bg-surface-2">
                <td className="px-4 py-3 text-fg-muted">{idx + 1}</td>
                <td className="px-4 py-3 font-medium text-fg">{item.name}</td>
                <td className="px-4 py-3 text-right text-fg">{item.quantity}</td>
                <td className="px-4 py-3 text-right text-fg">€{item.revenue.toFixed(2)}</td>
                <td className="px-4 py-3 hidden sm:table-cell">
                  <div className="h-1.5 w-full rounded-full bg-border overflow-hidden">
                    <div
                      className="h-full rounded-full bg-accent"
                      style={{ width: `${(item.quantity / maxQty) * 100}%` }}
                    />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
