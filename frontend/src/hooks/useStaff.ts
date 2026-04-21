// TODO: replace with real API call when backend is ready
import { useQuery } from "@tanstack/react-query";
import { getStaff } from "@/lib/api/staffApi";
import { queryKeys } from "@/lib/api/queryKeys";
import type { StaffMember } from "@/types/staff";

export interface UseStaffResult {
  staff: StaffMember[];
  isLoading: boolean;
  isError: boolean;
}

export function useStaff(): UseStaffResult {
  const { data: staff = [], isLoading, isError } = useQuery({
    queryKey: queryKeys.staff.list(),
    queryFn: getStaff,
  });

  return { staff, isLoading, isError };
}
