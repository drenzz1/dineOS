export type Role = "Manager" | "Cashier" | "KitchenStaff";

export interface StaffMember {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  pin: string;
}
