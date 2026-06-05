import { useQuery } from "@tanstack/react-query";
import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
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

async function fetchAdminAnalytics(): Promise<AdminAnalytics> {
  try {
    const res = await apiClient.get<ApiResponse<AdminAnalytics>>(
      "/v1/admin/analytics"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export function useAdminAnalytics() {
  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.adminAnalytics.all,
    queryFn: fetchAdminAnalytics,
  });

  return { analytics: data ?? null, isLoading, isError };
}
