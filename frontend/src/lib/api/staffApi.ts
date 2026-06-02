import apiClient from "@/lib/api/apiClient";
import type { StaffMember } from "@/types";
import type {
  StaffMemberFormValues,
  EditStaffMemberFormValues,
} from "@/lib/validations";
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
  data: StaffMemberFormValues | EditStaffMemberFormValues,
  id?: number
): Promise<StaffMember> {
  try {
    if (id !== undefined) {
      // On edit the PIN is optional: omit it when blank so the backend keeps
      // the existing PIN (UpdateStaffMemberRequest validates PIN only if sent).
      const { pin, ...rest } = data;
      const payload = pin ? { ...rest, pin } : rest;
      const res = await apiClient.put<ApiResponse<StaffMember>>(`/v1/staff/${id}`, payload);
      return unwrap(res);
    }
    const res = await apiClient.post<ApiResponse<StaffMember>>("/v1/staff", data);
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function setStaffActive(
  id: number,
  isActive: boolean
): Promise<StaffMember> {
  try {
    // The backend performs an absolute set (SetStaffActiveRequest.IsActive), so
    // the caller must send the desired state. A PATCH with no JSON body returns
    // 415 Unsupported Media Type, so the { isActive } body is required.
    const res = await apiClient.patch<ApiResponse<StaffMember>>(
      `/v1/staff/${id}/active`,
      { isActive }
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
