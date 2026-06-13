import { useMutation } from "@tanstack/react-query";
import { generateAdminBillingInsight } from "@/lib/api/aiApi";

export function useAdminAiBillingInsight() {
  return useMutation({ mutationFn: generateAdminBillingInsight });
}
