import type { Role } from "./staff";

export type UserStatus = "Active" | "Inactive" | "Suspended";

export interface AdminUser {
  id: number;
  name: string;
  email: string;
  role: Role | "SuperAdmin";
  restaurantName: string;
  status: UserStatus;
  lastLogin: string | null;
}
