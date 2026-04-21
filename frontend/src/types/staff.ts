export type Role = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

export interface StaffMember {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  pin: string;
  isActive: boolean;
}
