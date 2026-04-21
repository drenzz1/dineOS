import { Card } from "@/components/ui/Card";
import type { DailySummary } from "@/hooks/useDailySummary";

// ─── Skeleton ────────────────────────────────────────────────────────────────

function SummaryCardSkeleton() {
  return (
    <Card>
      <div className="animate-pulse space-y-3">
        <div className="h-3 w-24 rounded bg-zinc-200" />
        <div className="h-8 w-20 rounded bg-zinc-200" />
      </div>
    </Card>
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

interface SummaryCardProps {
  label: string;
  value: string;
  accent: string;
  subtext?: string;
}

function SummaryCard({ label, value, accent, subtext }: SummaryCardProps) {
  return (
    <Card>
      <p className="text-xs font-semibold uppercase tracking-wide text-zinc-400">
        {label}
      </p>
      <p className={`mt-2 text-2xl font-bold ${accent}`}>{value}</p>
      {subtext && (
        <p className="mt-1 text-xs text-zinc-400">{subtext}</p>
      )}
    </Card>
  );
}

interface SummaryCardsProps {
  summary: DailySummary;
}

export function SummaryCards({ summary }: SummaryCardsProps) {
  const cards: SummaryCardProps[] = [
    {
      label: "Total Orders",
      value: String(summary.totalOrders),
      accent: "text-blue-600",
      subtext: "all statuses",
    },
    {
      label: "Total Revenue",
      value: `$${summary.totalRevenue.toFixed(2)}`,
      accent: "text-green-600",
      subtext: "delivered orders only",
    },
    {
      label: "Cancelled",
      value: String(summary.cancelledOrders),
      accent:
        summary.cancelledOrders > 0 ? "text-red-600" : "text-zinc-400",
    },
    {
      label: "Avg Prep Time",
      value:
        summary.avgPrepTimeMinutes === 0
          ? "—"
          : `${summary.avgPrepTimeMinutes} min`,
      accent: "text-amber-600",
      subtext: "delivered orders only",
    },
  ];

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {cards.map((card) => (
        <SummaryCard key={card.label} {...card} />
      ))}
    </div>
  );
}
