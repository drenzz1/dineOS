"use client";

import { useState } from "react";
import { useOrderHistory } from "@/hooks/useReports";
import { Skeleton } from "@/components/ui/Skeleton";

interface HistoryTabProps {
  from: string;
  to: string;
}

const STATUS_STYLES: Record<string, string> = {
  New: "bg-status-new-bg text-status-new-fg border-status-new-border",
  InProgress: "bg-status-inprogress-bg text-status-inprogress-fg border-status-inprogress-border",
  Ready: "bg-status-ready-bg text-status-ready-fg border-status-ready-border",
  Delivered: "bg-status-delivered-bg text-status-delivered-fg border-status-delivered-border",
  Cancelled: "bg-status-cancelled-bg text-status-cancelled-fg border-status-cancelled-border",
};

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_STYLES[status] ?? "bg-surface-2 text-fg-muted border-border";
  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-semibold ${cls}`}>
      {status}
    </span>
  );
}

function formatDateTime(iso: string) {
  const d = new Date(iso);
  return d.toLocaleString("en-GB", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

const PAGE_SIZE = 25;

export default function HistoryTab({ from, to }: HistoryTabProps) {
  const [page, setPage] = useState(1);
  const { report, isLoading, isError } = useOrderHistory(from, to, page);

  if (isError) {
    return (
      <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3">
        <p className="text-[13px] text-status-cancelled-fg">
          Failed to load order history. Please refresh.
        </p>
      </div>
    );
  }

  if (isLoading || !report) {
    return (
      <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
        {Array.from({ length: 8 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  const totalPages = Math.ceil(report.totalCount / PAGE_SIZE);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-[13px] text-fg-muted">
          {report.totalCount} order{report.totalCount !== 1 ? "s" : ""} in this period
        </p>
      </div>

      <div className="bg-surface border border-border rounded-md shadow-sm overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="border-b border-border bg-surface-2">
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Date & Time</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Type</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Table</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Status</th>
              <th className="px-4 py-3 text-right font-semibold text-fg-muted">Items</th>
              <th className="px-4 py-3 text-right font-semibold text-fg-muted">Total</th>
              <th className="px-4 py-3 text-left font-semibold text-fg-muted">Payment</th>
            </tr>
          </thead>
          <tbody>
            {report.orders.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-fg-muted">
                  No orders in this period.
                </td>
              </tr>
            ) : (
              report.orders.map((order) => (
                <tr key={order.id} className="border-b border-border last:border-0 hover:bg-surface-2">
                  <td className="px-4 py-3 whitespace-nowrap text-fg">{formatDateTime(order.createdAt)}</td>
                  <td className="px-4 py-3 text-fg-muted">{order.orderType}</td>
                  <td className="px-4 py-3 text-fg-muted">
                    {order.tableNumber != null ? `#${order.tableNumber}` : "—"}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={order.status} />
                  </td>
                  <td className="px-4 py-3 text-right text-fg-muted">{order.itemCount}</td>
                  <td className="px-4 py-3 text-right font-medium text-fg">
                    €{order.total.toFixed(2)}
                  </td>
                  <td className="px-4 py-3 text-fg-muted">{order.paymentMethod ?? "—"}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
            className="h-8 rounded-md border border-border bg-surface px-3 text-[12px] font-medium text-fg disabled:opacity-40 hover:bg-surface-2 disabled:cursor-not-allowed"
          >
            Previous
          </button>
          <span className="text-[12px] text-fg-muted">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
            className="h-8 rounded-md border border-border bg-surface px-3 text-[12px] font-medium text-fg disabled:opacity-40 hover:bg-surface-2 disabled:cursor-not-allowed"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
