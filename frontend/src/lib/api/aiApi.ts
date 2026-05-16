import apiClient from "@/lib/api/apiClient";
import type { MenuItemDescriptionSuggestion } from "@/types/menu";
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
