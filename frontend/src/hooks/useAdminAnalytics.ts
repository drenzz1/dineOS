// TODO: replace with real API call when backend is ready
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";

export interface WeeklyGrowth {
  week: string; // e.g. "Mar 3"
  newRestaurants: number;
}

export interface TopRestaurant {
  rank: number;
  name: string;
  orders: number;
  revenue: number;
}

export interface ActivityEvent {
  id: string;
  description: string;
  timestamp: string; // ISO
}

export interface AdminAnalytics {
  totalRestaurants: number;
  activeRestaurants: number;
  ordersToday: number;
  revenueToday: number;
  weeklyGrowth: WeeklyGrowth[];
  topRestaurants: TopRestaurant[];
  activityFeed: ActivityEvent[];
}

function ago(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

const MOCK_ANALYTICS: AdminAnalytics = {
  totalRestaurants: 12,
  activeRestaurants: 9,
  ordersToday: 47,
  revenueToday: 2340,
  weeklyGrowth: [
    { week: "Mar 3", newRestaurants: 1 },
    { week: "Mar 10", newRestaurants: 0 },
    { week: "Mar 17", newRestaurants: 2 },
    { week: "Mar 24", newRestaurants: 1 },
    { week: "Mar 31", newRestaurants: 3 },
    { week: "Apr 7", newRestaurants: 1 },
    { week: "Apr 14", newRestaurants: 2 },
    { week: "Apr 21", newRestaurants: 2 },
  ],
  topRestaurants: [
    { rank: 1, name: "Ora Restaurant", orders: 312, revenue: 18740 },
    { rank: 2, name: "Kroi Bistro", orders: 278, revenue: 15600 },
    { rank: 3, name: "Pjata Shqiptare", orders: 241, revenue: 13200 },
    { rank: 4, name: "Brasserie 44", orders: 198, revenue: 11050 },
    { rank: 5, name: "Garden Café", orders: 154, revenue: 8320 },
  ],
  activityFeed: [
    {
      id: "evt-001",
      description: "New restaurant registered: Garden Café",
      timestamp: ago(12),
    },
    {
      id: "evt-002",
      description: "Order milestone reached: Ora Restaurant hit 300 orders",
      timestamp: ago(47),
    },
    {
      id: "evt-003",
      description: "New restaurant registered: Brasserie 44",
      timestamp: ago(130),
    },
    {
      id: "evt-004",
      description: "Restaurant suspended: Old Town Grill",
      timestamp: ago(310),
    },
    {
      id: "evt-005",
      description: "Plan upgraded to Pro: Kroi Bistro",
      timestamp: ago(520),
    },
  ],
};

async function fetchAdminAnalytics(): Promise<AdminAnalytics> {
  await new Promise<void>((resolve) => setTimeout(resolve, 400));
  return MOCK_ANALYTICS;
}

export function useAdminAnalytics() {
  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.adminAnalytics.all,
    queryFn: fetchAdminAnalytics,
  });

  return { analytics: data ?? null, isLoading, isError };
}
