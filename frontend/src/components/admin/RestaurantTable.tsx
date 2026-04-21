import Link from "next/link";
import type { Restaurant, RestaurantPlan, RestaurantStatus } from "@/types";

const PLAN_BADGE: Record<RestaurantPlan, string> = {
  Free: "bg-zinc-100 text-zinc-700",
  Pro: "bg-blue-100 text-blue-700",
};

const STATUS_BADGE: Record<RestaurantStatus, string> = {
  Active: "bg-green-100 text-green-700",
  Suspended: "bg-amber-100 text-amber-700",
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
      <p role="status" className="text-sm text-zinc-500">
        Loading restaurants…
      </p>
    );
  }

  if (restaurants.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-zinc-300 p-8 text-center">
        <p className="text-sm text-zinc-500">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200">
      <table className="w-full text-sm text-zinc-900">
        <caption className="sr-only">Restaurants</caption>
        <thead className="border-b border-zinc-200 bg-zinc-50">
          <tr>
            {COLUMNS.map((col, i) => (
              <th
                key={i}
                scope="col"
                className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100">
          {restaurants.map((r) => (
            <tr key={r.id} className="hover:bg-zinc-50">
              <th
                scope="row"
                className="px-4 py-3 text-left font-medium text-zinc-900"
              >
                {r.name}
              </th>
              <td className="px-4 py-3 text-zinc-600">{r.ownerEmail}</td>
              <td className="px-4 py-3">
                <Badge label={r.plan} className={PLAN_BADGE[r.plan]} />
              </td>
              <td className="px-4 py-3">
                <Badge label={r.status} className={STATUS_BADGE[r.status]} />
              </td>
              <td className="px-4 py-3 text-zinc-600">
                {r.totalOrders.toLocaleString()}
              </td>
              <td className="px-4 py-3 text-zinc-500">{formatDate(r.createdAt)}</td>
              <td className="px-4 py-3">
                <Link
                  href={`/admin/restaurants/${r.id}`}
                  className="text-xs font-medium text-blue-600 hover:text-blue-800 hover:underline"
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
