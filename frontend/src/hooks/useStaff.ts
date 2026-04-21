// TODO: replace with real API call when backend is ready
import { useQuery } from "@tanstack/react-query";
import { getStaff } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import type { StaffMember } from "@/types/staff";

export interface UseStaffResult {
  staff: StaffMember[];
  isLoading: boolean;
  isError: boolean;
}

export function useStaff(): UseStaffResult {
  const { tenantId } = useTenant();
  const { data: staff = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.staff.list(tenantId),
    queryFn: getStaff,
  });

  return { staff, isLoading, isError };
}
