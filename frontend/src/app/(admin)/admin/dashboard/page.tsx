"use client";

import dynamic from "next/dynamic";
import { useAdminAnalytics } from "@/hooks/useAdminAnalytics";
import PlatformSummaryCards from "@/components/admin/PlatformSummaryCards";
import TopRestaurantsTable from "@/components/admin/TopRestaurantsTable";
import ActivityFeed from "@/components/admin/ActivityFeed";

const RestaurantGrowthChart = dynamic(
  () => import("@/components/admin/RestaurantGrowthChart"),
  {
    ssr: false,
    loading: () => (
      <div className="h-[268px] animate-pulse rounded-lg bg-zinc-100" />
    ),
  }
);

export default function AdminDashboardPage() {
  const { analytics, isLoading, isError } = useAdminAnalytics();

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-semibold text-zinc-900">Dashboard</h1>
        <p className="mt-0.5 text-sm text-zinc-500">
          Platform-wide overview for all restaurants.
        </p>
      </div>

      {/* Error */}
      {isError && (
        <div className="rounded-md bg-red-50 px-4 py-3">
          <p className="text-sm text-red-600">
            Failed to load analytics. Please refresh.
          </p>
        </div>
      )}

      {/* Metric cards */}
      <PlatformSummaryCards analytics={analytics} isLoading={isLoading} />

      {/* Chart + Activity side by side on large screens */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <RestaurantGrowthChart
            data={analytics?.weeklyGrowth ?? null}
            isLoading={isLoading}
          />
        </div>
        <div>
          <ActivityFeed
            events={analytics?.activityFeed ?? null}
            isLoading={isLoading}
          />
        </div>
      </div>

      {/* Top restaurants */}
      <TopRestaurantsTable
        restaurants={analytics?.topRestaurants ?? null}
        isLoading={isLoading}
      />
    </div>
  );
}
