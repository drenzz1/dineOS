import type { Role } from "./staff";
import type { AiSuggestionMetadata } from "./menu";

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

export type AdminBillingInsight = {
  narrative: string;
  metadata: AiSuggestionMetadata;
};
