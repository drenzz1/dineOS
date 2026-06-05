import { useQuery } from "@tanstack/react-query";
import { getMe } from "@/lib/api/meApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useAuthStore } from "@/stores/authStore";
import type { MeResponse } from "@/types/me";

export interface UseMeResult {
  user: MeResponse | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useMe(): UseMeResult {
  const accessToken = useAuthStore((s) => s.accessToken);

  const { data: user, isLoading, isError } = useQuery({
    queryKey: queryKeys.me.current(),
    queryFn: getMe,
    staleTime: 5 * 60 * 1000,
    enabled: accessToken !== null,
  });

  return { user, isLoading, isError };
}
