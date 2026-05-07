export type Role = "Manager" | "Cashier" | "KitchenStaff";

export interface StaffMember {
  id: number;
  fullName: string;
  email: string;
  role: Role;
  isActive: boolean;
  tenantId: number;
}
