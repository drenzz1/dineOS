import axios from "axios";
import apiClient from "@/lib/api/apiClient";
import type { MenuItem, MenuItemDto, MenuCategoryDto } from "@/types/menu";
import type { MenuItemFormValues } from "@/lib/validations/menuItem";
import { type ApiResponse, unwrap, toApiError, ApiError } from "@/lib/api/envelope";

// ─── Mapper ───────────────────────────────────────────────────────────────────

function mapMenuItem(dto: MenuItemDto): MenuItem {
  return {
    id: String(dto.id),
    tenantId: String(dto.tenantId),
    name: dto.name,
    price: dto.price,
    category: dto.category,
    description: dto.description ?? undefined,
    imageUrl: dto.imageUrl ?? undefined,
  };
}

// ─── Items ────────────────────────────────────────────────────────────────────

export async function getMenuItems(): Promise<MenuItem[]> {
  try {
    const res = await apiClient.get<ApiResponse<MenuItemDto[]>>("/v1/menu/items");
    return unwrap(res).map(mapMenuItem);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function saveMenuItem(
  data: MenuItemFormValues,
  id?: string,
  existingImageUrl?: string
): Promise<MenuItem> {
  const body = {
    name: data.name,
    price: data.price,
    category: data.category,
    description: data.description || null,
    imageUrl: existingImageUrl ?? null,
  };
  try {
    if (id !== undefined) {
      const res = await apiClient.put<ApiResponse<MenuItemDto>>(`/v1/menu/items/${id}`, body);
      return mapMenuItem(unwrap(res));
    }
    const res = await apiClient.post<ApiResponse<MenuItemDto>>("/v1/menu/items", body);
    return mapMenuItem(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function deleteMenuItem(id: string): Promise<void> {
  try {
    const res = await apiClient.delete<ApiResponse<MenuItemDto>>(`/v1/menu/items/${id}`);
    unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

// ─── Image upload ─────────────────────────────────────────────────────────────

const IMAGE_ERROR_MESSAGES: Record<string, string> = {
  FILE_EMPTY: "File is empty",
  FILE_TOO_LARGE: "File is too large",
  UNSUPPORTED_CONTENT_TYPE: "Unsupported file type",
  INVALID_EXTENSION: "Invalid file extension",
  EXTENSION_MISMATCH: "File extension doesn't match content",
};

export async function uploadMenuItemImage(id: string, file: File): Promise<string> {
  const formData = new FormData();
  formData.append("image", file);
  try {
    const res = await apiClient.post<ApiResponse<{ imageUrl: string }>>(
      `/v1/menu/items/${id}/image`,
      formData
    );
    return unwrap(res).imageUrl;
  } catch (error) {
    // Image upload returns ValidationProblemDetails (RFC 7807) on 400,
    // not the standard ApiResponse, so we map error codes here.
    if (axios.isAxiosError(error) && error.response?.status === 400) {
      const body = error.response.data as {
        errors?: Record<string, string[]>;
        title?: string;
      };
      if (body?.errors && !Array.isArray(body.errors)) {
        const codes = Object.values(body.errors).flat();
        const message =
          codes.map((c) => IMAGE_ERROR_MESSAGES[c] ?? c).join(". ") ||
          body.title ||
          "Image upload failed";
        throw new ApiError({ error: message, errors: codes, status: 400 });
      }
    }
    throw toApiError(error);
  }
}

// ─── Categories ───────────────────────────────────────────────────────────────

export async function getCategories(): Promise<string[]> {
  try {
    const res = await apiClient.get<ApiResponse<MenuCategoryDto[]>>("/v1/menu/categories");
    return unwrap(res).map((c) => c.name);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function addCategory(name: string): Promise<string> {
  try {
    const res = await apiClient.post<ApiResponse<MenuCategoryDto>>("/v1/menu/categories", { name });
    return unwrap(res).name;
  } catch (error) {
    throw toApiError(error);
  }
}
