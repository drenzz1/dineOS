import type { Role } from "./staff";

export type UserStatus = "Active" | "Inactive" | "Suspended";

export interface AdminUser {
  id: string;
  name: string;
  email: string;
  role: Role | "SuperAdmin";
  restaurantName: string | null; // null for SuperAdmin — no tenant scope
  status: UserStatus;
  lastLogin: string | null; // ISO string; null if never logged in
}
