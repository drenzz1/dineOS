import { useAuthStore } from "@/stores/authStore";

export function useTenant() {
  const tenantId = useAuthStore((s) => s.tenantId);
  const restaurantName = useAuthStore((s) => s.restaurantName);
  return { tenantId, restaurantName };
}
