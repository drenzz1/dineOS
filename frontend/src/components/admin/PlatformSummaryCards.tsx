import { Card } from "@/components/ui/Card";
import type { AdminAnalytics } from "@/hooks/useAdminAnalytics";

interface MetricCardProps {
  label: string;
  value: string;
  sub?: string;
}

function MetricCard({ label, value, sub }: MetricCardProps) {
  return (
    <Card>
      <p className="text-xs font-semibold uppercase tracking-wide text-zinc-400">
        {label}
      </p>
      <p className="mt-1.5 text-3xl font-bold text-zinc-900">{value}</p>
      {sub && <p className="mt-0.5 text-xs text-zinc-500">{sub}</p>}
    </Card>
  );
}

function MetricCardSkeleton() {
  return (
    <Card className="animate-pulse space-y-2">
      <div className="h-3 w-28 rounded bg-zinc-200" />
      <div className="h-8 w-20 rounded bg-zinc-200" />
      <div className="h-3 w-16 rounded bg-zinc-100" />
    </Card>
  );
}

interface PlatformSummaryCardsProps {
  analytics: AdminAnalytics | null;
  isLoading: boolean;
}

export default function PlatformSummaryCards({
  analytics,
  isLoading,
}: PlatformSummaryCardsProps) {
  if (isLoading || !analytics) {
    return (
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <MetricCardSkeleton key={i} />
        ))}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <MetricCard
        label="Total Restaurants"
        value={analytics.totalRestaurants.toString()}
        sub={`${analytics.activeRestaurants} active`}
      />
      <MetricCard
        label="Active Restaurants"
        value={analytics.activeRestaurants.toString()}
        sub={`${analytics.totalRestaurants - analytics.activeRestaurants} suspended`}
      />
      <MetricCard
        label="Orders Today"
        value={analytics.ordersToday.toString()}
      />
      <MetricCard
        label="Revenue Today"
        value={`$${analytics.revenueToday.toLocaleString()}`}
      />
    </div>
  );
}
