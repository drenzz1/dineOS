import { Card } from "@/components/ui/Card";
import type { TopRestaurant } from "@/hooks/useAdminAnalytics";

function TableSkeleton() {
  return (
    <Card className="animate-pulse space-y-3">
      <div className="h-5 w-40 rounded bg-zinc-200" />
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-10 rounded bg-zinc-100" />
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
      <div className="border-b border-zinc-200 px-4 py-3">
        <h2 className="text-sm font-semibold text-zinc-900">
          Top Restaurants — This Month
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[400px] text-sm">
          <thead className="border-b border-zinc-100 bg-zinc-50">
            <tr>
              {["#", "Restaurant", "Orders", "Revenue"].map((col) => (
                <th
                  key={col}
                  className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500"
                >
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100">
            {restaurants.map((r) => (
              <tr key={r.rank} className="hover:bg-zinc-50">
                <td className="px-4 py-3 font-semibold text-zinc-400">
                  {r.rank}
                </td>
                <td className="px-4 py-3 font-medium text-zinc-900">
                  {r.name}
                </td>
                <td className="px-4 py-3 text-zinc-700">
                  {r.orders.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-zinc-700">
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
