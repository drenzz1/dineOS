import { Card } from "@/components/ui/Card";
import type { TopRestaurant } from "@/hooks/useAdminAnalytics";

function TableSkeleton() {
  return (
    <Card className="animate-pulse space-y-3">
      <div className="h-5 w-40 rounded bg-surface-3" />
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-10 rounded bg-surface-2" />
        ))}
      </div>
    </Card>
  );
}

interface TopRestaurantsTableProps {
  restaurants: TopRestaurant[] | null;
  isLoading: boolean;
}

export default function TopRestaurantsTable({
  restaurants,
  isLoading,
}: TopRestaurantsTableProps) {
  if (isLoading || !restaurants) return <TableSkeleton />;

  return (
    <Card className="overflow-hidden p-0">
      <div className="border-b border-border px-4 py-3">
        <h2 className="text-sm font-semibold text-fg">
          Top Restaurants — This Month
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[400px] text-sm">
          <thead className="border-b border-border bg-surface-2">
            <tr>
              {["#", "Restaurant", "Orders", "Revenue"].map((col) => (
                <th
                  key={col}
                  className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-fg-subtle"
                >
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {restaurants.map((r) => (
              <tr key={r.rank} className="hover:bg-surface-2">
                <td className="px-4 py-3 font-semibold text-fg-subtle">
                  {r.rank}
                </td>
                <td className="px-4 py-3 font-medium text-fg">
                  {r.name}
                </td>
                <td className="px-4 py-3 text-fg-muted">
                  {r.orders.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-fg-muted">
                  ${r.revenue.toLocaleString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}
