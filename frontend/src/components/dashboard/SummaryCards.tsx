import { Stat } from "@/components/ui/Stat";
import { Skeleton } from "@/components/ui/Skeleton";
import type { DailySummary } from "@/hooks/useDailySummary";

// ─── Skeleton ────────────────────────────────────────────────────────────────

function SummaryCardSkeleton() {
  return (
    <div className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-3">
      <div className="flex items-center justify-between">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-4 w-12 rounded-full" />
      </div>
      <Skeleton className="h-7 w-20" />
      <Skeleton className="h-3 w-28" />
    </div>
  );
}

export function SummaryCardsSkeleton() {
  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {[0, 1, 2, 3].map((i) => (
        <SummaryCardSkeleton key={i} />
      ))}
    </div>
  );
}

// ─── Cards ────────────────────────────────────────────────────────────────────

interface SummaryCardsProps {
  summary: DailySummary;
}

export function SummaryCards({ summary }: SummaryCardsProps) {
  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <Stat
        label="Total Orders"
        value={summary.totalOrders}
        sub="all statuses"
      />
      <Stat
        label="Total Revenue"
        value={`$${summary.totalRevenue.toFixed(2)}`}
        sub="delivered orders only"
        trend="up"
      />
      <Stat
        label="Cancelled"
        value={summary.cancelledOrders}
        trend={summary.cancelledOrders > 0 ? "down" : "flat"}
      />
      <Stat
        label="Avg Prep Time"
        value={
          summary.avgPrepTimeMinutes === 0
            ? "—"
            : `${summary.avgPrepTimeMinutes}m`
        }
        sub="delivered orders only"
      />
    </div>
  );
}
