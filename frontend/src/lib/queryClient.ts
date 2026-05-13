import { QueryClient, QueryCache, MutationCache } from "@tanstack/react-query";
import { handleApiError } from "@/lib/api/errorToast";

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error) => handleApiError(error),
  }),
  mutationCache: new MutationCache({
    onError: (error) => handleApiError(error),
  }),
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: 1,
    },
  },
});
