import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getAiSettings,
  saveAiSettings,
  saveEmbeddingsSettings,
  testAiConnection,
} from "@/lib/api/aiSettingsApi";
import { queryKeys } from "@/lib/api/queryKeys";

export function useAiSettings() {
  return useQuery({
    queryKey: queryKeys.adminAiSettings.all,
    queryFn: getAiSettings,
  });
}

export function useSaveAiSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: saveAiSettings,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: queryKeys.adminAiSettings.all }),
  });
}

export function useSaveEmbeddingsSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: saveEmbeddingsSettings,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: queryKeys.adminAiSettings.all }),
  });
}

export function useTestAiConnection() {
  return useMutation({ mutationFn: testAiConnection });
}
