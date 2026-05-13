import type { Role } from "@/types/staff";

export type AppRole = Role | "SuperAdmin";

export interface MeResponse {
  id: string;
  email: string;
  username: string;
  name: string;
  roles: string[];
  tenantId: string | null;
}
