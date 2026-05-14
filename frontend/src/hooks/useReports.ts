import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { getSalesReport, getOrdersReport, getStaffReport } from "@/lib/api/reportsApi";
import type { SalesReport, OrdersReport, StaffReport } from "@/types/reports";

export interface UseSalesReportResult {
  report: SalesReport | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useSalesReport(from: string, to: string): UseSalesReportResult {
  const { tenantId } = useTenant();
  const { data: report, isLoading, isError } = useQuery({
    queryKey: queryKeys.reports.sales(tenantId, from, to),
    queryFn: () => getSalesReport(from, to),
  });
  return { report, isLoading, isError };
}

export interface UseOrdersReportResult {
  report: OrdersReport | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useOrdersReport(from: string, to: string): UseOrdersReportResult {
  const { tenantId } = useTenant();
  const { data: report, isLoading, isError } = useQuery({
    queryKey: queryKeys.reports.orders(tenantId, from, to),
    queryFn: () => getOrdersReport(from, to),
  });
  return { report, isLoading, isError };
}

export interface UseStaffReportResult {
  report: StaffReport | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useStaffReport(): UseStaffReportResult {
  const { tenantId } = useTenant();
  const { data: report, isLoading, isError } = useQuery({
    queryKey: queryKeys.reports.staff(tenantId),
    queryFn: getStaffReport,
  });
  return { report, isLoading, isError };
}
