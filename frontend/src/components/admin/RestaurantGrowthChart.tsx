import dynamic from "next/dynamic";
import { Card } from "@/components/ui/Card";
import type { WeeklyGrowth } from "@/hooks/useAdminAnalytics";

const RestaurantGrowthChartInner = dynamic(
  () => import("./RestaurantGrowthChartInner"),
  {
    ssr: false,
    loading: () => (
      <div className="h-[220px] animate-pulse rounded-md bg-zinc-100" />
    ),
  }
);

function ChartSkeleton() {
  return (
    <Card className="animate-pulse space-y-3">
      <div className="h-5 w-48 rounded bg-zinc-200" />
      <div className="h-[220px] rounded-md bg-zinc-100" />
    </Card>
  );
}

interface RestaurantGrowthChartProps {
  data: WeeklyGrowth[] | null;
  isLoading: boolean;
}

export default function RestaurantGrowthChart({
  data,
  isLoading,
}: RestaurantGrowthChartProps) {
  if (isLoading || !data) return <ChartSkeleton />;

  return (
    <Card className="space-y-3">
      <h2 className="text-sm font-semibold text-zinc-900">
        New Restaurants — Last 8 Weeks
      </h2>
      <RestaurantGrowthChartInner data={data} />
    </Card>
  );
}
