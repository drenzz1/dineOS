import { useAuthStore } from "@/stores/authStore";
import { useMe } from "@/hooks/useMe";

export function useTenant() {
  const { user } = useMe();
  const storedTenantId = useAuthStore((s) => s.tenantId);
  const restaurantName = useAuthStore((s) => s.restaurantName);
  return { tenantId: user?.tenantId ?? storedTenantId, restaurantName };
}
