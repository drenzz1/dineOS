import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import {
  getSalesReport,
  getOrdersReport,
  getStaffReport,
  getItemsReport,
  getOrderHistory,
} from "@/lib/api/reportsApi";
import type { SalesReport, OrdersReport, StaffReport, ItemsReport, OrderHistoryReport } from "@/types/reports";

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

export interface UseItemsReportResult {
  report: ItemsReport | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useItemsReport(from: string, to: string): UseItemsReportResult {
  const { tenantId } = useTenant();
  const { data: report, isLoading, isError } = useQuery({
    queryKey: queryKeys.reports.items(tenantId, from, to),
    queryFn: () => getItemsReport(from, to),
  });
  return { report, isLoading, isError };
}

export interface UseOrderHistoryResult {
  report: OrderHistoryReport | undefined;
  isLoading: boolean;
  isError: boolean;
}

export function useOrderHistory(from: string, to: string, page: number): UseOrderHistoryResult {
  const { tenantId } = useTenant();
  const { data: report, isLoading, isError } = useQuery({
    queryKey: queryKeys.reports.history(tenantId, from, to, page),
    queryFn: () => getOrderHistory(from, to, page),
  });
  return { report, isLoading, isError };
}
