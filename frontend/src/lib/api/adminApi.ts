import apiClient from "@/lib/api/apiClient";
import type { AdminUser } from "@/types";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

interface PlatformUserDto {
  id: number;
  fullName: string;
  email: string;
  role: string;
  tenantName: string;
  isActive: boolean;
}

interface PagedData {
  items: PlatformUserDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface AdminUsersParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminUsersResponse {
  users: AdminUser[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

function mapUser(dto: PlatformUserDto): AdminUser {
  return {
    id: dto.id,
    name: dto.fullName,
    email: dto.email,
    role: dto.role as AdminUser["role"],
    restaurantName: dto.tenantName,
    status: dto.isActive ? "Active" : "Inactive",
    lastLogin: null,
  };
}

export async function getAdminUsers(
  params: AdminUsersParams = {}
): Promise<AdminUsersResponse> {
  try {
    const res = await apiClient.get<ApiResponse<PagedData>>("/v1/admin/users", { params });
    const { items, ...pagination } = unwrap(res);
    return { users: items.map(mapUser), ...pagination };
  } catch (error) {
    throw toApiError(error);
  }
}
