import apiClient from "@/lib/api/apiClient";
import type { StaffMember } from "@/types";
import type { StaffMemberFormValues } from "@/lib/validations";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export async function getStaff(): Promise<StaffMember[]> {
  try {
    const res = await apiClient.get<ApiResponse<StaffMember[]>>("/v1/staff");
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function saveStaffMember(
  data: StaffMemberFormValues,
  id?: number
): Promise<StaffMember> {
  try {
    if (id !== undefined) {
      const res = await apiClient.put<ApiResponse<StaffMember>>(`/v1/staff/${id}`, data);
      return unwrap(res);
    }
    const res = await apiClient.post<ApiResponse<StaffMember>>("/v1/staff", data);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function toggleStaffActive(id: number): Promise<StaffMember> {
  try {
    const res = await apiClient.patch<ApiResponse<StaffMember>>(`/v1/staff/${id}/active`);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
