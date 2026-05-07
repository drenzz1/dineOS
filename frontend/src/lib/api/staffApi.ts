import apiClient from "@/lib/api/apiClient";
import type { StaffMember } from "@/types";
import type { StaffMemberFormValues } from "@/lib/validations";

// GET /api/v1/staff
export async function getStaff(): Promise<StaffMember[]> {
  const res = await apiClient.get<{ data: StaffMember[] }>("/v1/staff");
  return res.data.data;
}

// POST /api/v1/staff (create) or PUT /api/v1/staff/:id (update)
export async function saveStaffMember(
  data: StaffMemberFormValues,
  id?: number
): Promise<StaffMember> {
  if (id !== undefined) {
    const res = await apiClient.put<{ data: StaffMember }>(`/v1/staff/${id}`, data);
    return res.data.data;
  }
  const res = await apiClient.post<{ data: StaffMember }>("/v1/staff", data);
  return res.data.data;
}

// PATCH /api/v1/staff/:id/active
export async function toggleStaffActive(id: number): Promise<StaffMember> {
  const res = await apiClient.patch<{ data: StaffMember }>(`/v1/staff/${id}/active`);
  return res.data.data;
}
