import type { StaffMember } from "@/types";
import type { StaffMemberFormValues } from "@/lib/validations/staffMember";

let mockStaff: StaffMember[] = [];

export async function saveStaffMember(
  data: StaffMemberFormValues,
  id?: string
): Promise<StaffMember> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  if (id) {
    const updated: StaffMember = { id, ...data };
    mockStaff = mockStaff.map((s) => (s.id === id ? updated : s));
    return updated;
  }
  const created: StaffMember = { id: crypto.randomUUID(), ...data };
  mockStaff = [...mockStaff, created];
  return created;
}

export async function getStaff(): Promise<StaffMember[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockStaff;
}
