export type Role = "Manager" | "Cashier" | "KitchenStaff";

export interface StaffMember {
  id: number;
  fullName: string;
  email: string;
  role: Role;
  isActive: boolean;
  tenantId: number;
}

// Result of a successful PIN verification (#staff-pin-auth Phase 3): a
// role-scoped staff-session token and the identity it represents.
export interface StaffSession {
  accessToken: string;
  expiresIn: number;
  staffMemberId: number;
  fullName: string;
  role: Role;
}
