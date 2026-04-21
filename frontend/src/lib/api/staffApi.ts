// TODO: replace with real API call when backend is ready
import type { StaffMember } from "@/types";
import type { StaffMemberFormValues } from "@/lib/validations/staffMember";

let mockStaff: StaffMember[] = [
  {
    id: "staff-001",
    fullName: "Ana Berisha",
    email: "ana@dineos.com",
    role: "Manager",
    pin: "1234",
    isActive: true,
  },
  {
    id: "staff-002",
    fullName: "Luan Krasniqi",
    email: "luan@dineos.com",
    role: "Cashier",
    pin: "5678",
    isActive: true,
  },
  {
    id: "staff-003",
    fullName: "Bjorn Haxhiu",
    email: "bjorn@dineos.com",
    role: "KitchenStaff",
    pin: "9012",
    isActive: true,
  },
  {
    id: "staff-004",
    fullName: "Drita Morina",
    email: "drita@dineos.com",
    role: "Cashier",
    pin: "3456",
    isActive: true,
  },
  {
    id: "staff-005",
    fullName: "Valdrin Gashi",
    email: "valdrin@dineos.com",
    role: "KitchenStaff",
    pin: "7890",
    isActive: false,
  },
];

export async function getStaff(): Promise<StaffMember[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return [...mockStaff];
}

export async function saveStaffMember(
  data: StaffMemberFormValues,
  id?: string
): Promise<StaffMember> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  if (id) {
    const existing = mockStaff.find((s) => s.id === id);
    const updated: StaffMember = {
      id,
      isActive: existing?.isActive ?? true,
      ...data,
    };
    mockStaff = mockStaff.map((s) => (s.id === id ? updated : s));
    return updated;
  }
  const created: StaffMember = {
    id: crypto.randomUUID(),
    isActive: true,
    ...data,
  };
  mockStaff = [...mockStaff, created];
  return created;
}

export async function toggleStaffActive(id: string): Promise<StaffMember> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  const member = mockStaff.find((s) => s.id === id);
  if (!member) throw new Error(`Staff member ${id} not found`);
  const updated: StaffMember = { ...member, isActive: !member.isActive };
  mockStaff = mockStaff.map((s) => (s.id === id ? updated : s));
  return updated;
}
