import apiClient from "@/lib/api/apiClient";
import type { MenuItemDescriptionSuggestion } from "@/types/menu";
import type { AdminBillingInsight } from "@/types/admin";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export async function describeMenuItem(id: string): Promise<MenuItemDescriptionSuggestion> {
  try {
    const res = await apiClient.post<ApiResponse<MenuItemDescriptionSuggestion>>(
      `/v1/ai/menu-items/${id}/describe`
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function generateAdminBillingInsight(): Promise<AdminBillingInsight> {
  try {
    const res = await apiClient.post<ApiResponse<AdminBillingInsight>>(
      "/v1/admin/analytics/ai-summary"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
