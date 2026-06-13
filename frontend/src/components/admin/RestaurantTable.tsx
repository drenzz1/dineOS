import Link from "next/link";
import type { Restaurant, RestaurantPlan, RestaurantStatus } from "@/types";

const PLAN_BADGE: Record<RestaurantPlan, string> = {
  Free: "bg-surface-2 text-fg-muted",
  Pro: "bg-status-new-bg text-info",
};

const STATUS_BADGE: Record<RestaurantStatus, string> = {
  Active: "bg-status-ready-bg text-success",
  Suspended: "bg-status-progress-bg text-warning",
};

function Badge({ label, className }: { label: string; className: string }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${className}`}
    >
      {label}
    </span>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

interface RestaurantTableProps {
  restaurants: Restaurant[];
  isLoading?: boolean;
  emptyMessage?: string;
}

const COLUMNS = [
  "Name",
  "Owner Email",
  "Plan",
  "Status",
  "Total Orders",
  "Date Joined",
  "",
] as const;

export default function RestaurantTable({
  restaurants,
  isLoading = false,
  emptyMessage = "No restaurants found.",
}: RestaurantTableProps) {
  if (isLoading) {
    return (
      <p role="status" className="text-sm text-fg-subtle">
        Loading restaurants…
      </p>
    );
  }

  if (restaurants.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border-strong p-8 text-center">
        <p className="text-sm text-fg-subtle">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full text-sm text-fg">
        <caption className="sr-only">Restaurants</caption>
        <thead className="border-b border-border bg-surface-2">
          <tr>
            {COLUMNS.map((col, i) => (
              <th
                key={i}
                scope="col"
                className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-fg-subtle"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {restaurants.map((r) => (
            <tr key={r.id} className="hover:bg-surface-2">
              <th
                scope="row"
                className="px-4 py-3 text-left font-medium text-fg"
              >
                {r.name}
              </th>
              <td className="px-4 py-3 text-fg-muted">{r.ownerEmail}</td>
              <td className="px-4 py-3">
                <Badge label={r.plan} className={PLAN_BADGE[r.plan]} />
              </td>
              <td className="px-4 py-3">
                <Badge label={r.status} className={STATUS_BADGE[r.status]} />
              </td>
              <td className="px-4 py-3 text-fg-muted">
                {r.totalOrders.toLocaleString()}
              </td>
              <td className="px-4 py-3 text-fg-subtle">{formatDate(r.createdAt)}</td>
              <td className="px-4 py-3">
                <Link
                  href={`/admin/restaurants/${r.id}`}
                  className="text-xs font-medium text-info hover:text-info hover:underline"
                >
                  View
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
