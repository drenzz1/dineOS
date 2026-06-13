"use client";

import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from "recharts";
import { useSalesReport } from "@/hooks/useReports";
import { Stat } from "@/components/ui/Stat";
import { DataTable } from "@/components/ui/DataTable";
import { Skeleton } from "@/components/ui/Skeleton";

interface SalesTabProps {
  from: string;
  to: string;
}

function formatShortDate(dateStr: string) {
  const d = new Date(dateStr + "T00:00:00");
  return d.toLocaleDateString("en-GB", { month: "short", day: "numeric" });
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
        <div className="bg-surface border border-border rounded-md shadow-sm p-4">
          <Skeleton className="h-48 w-full" />
        </div>
        <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
      </div>
    );
  }

  const chartData = report.revenueByDay.map((d) => ({
    date: formatShortDate(d.date),
    revenue: d.revenue,
    orders: d.orderCount,
  }));

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

      {chartData.length > 0 && (
        <div className="bg-surface border border-border rounded-md shadow-sm p-4">
          <h2 className="text-[13px] font-semibold text-fg mb-4">Revenue Over Time</h2>
          <ResponsiveContainer width="100%" height={200}>
            <LineChart data={chartData} margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis
                dataKey="date"
                tick={{ fontSize: 11, fill: "var(--fg-muted)" }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 11, fill: "var(--fg-muted)" }}
                axisLine={false}
                tickLine={false}
                tickFormatter={(v: number) => `€${v}`}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: "var(--surface)",
                  border: "1px solid var(--border)",
                  borderRadius: "6px",
                  fontSize: "12px",
                  color: "var(--fg)",
                }}
                formatter={(value) => [`€${Number(value).toFixed(2)}`, "Revenue"]}
              />
              <Line
                type="monotone"
                dataKey="revenue"
                stroke="var(--accent)"
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 4 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

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
