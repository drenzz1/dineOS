import { useMutation } from "@tanstack/react-query";
import { semanticSearchMenuItems } from "@/lib/api/menuApi";

export function useSemanticMenuSearch() {
  return useMutation({
    mutationFn: semanticSearchMenuItems,
  });
}
