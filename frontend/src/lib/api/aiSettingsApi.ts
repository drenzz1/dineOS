import apiClient from "@/lib/api/apiClient";
import type {
  AiSettings,
  SaveAiSettingsRequest,
  TestAiConnectionRequest,
  TestAiConnectionResult,
} from "@/types/admin";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";

export async function getAiSettings(): Promise<AiSettings> {
  try {
    const res = await apiClient.get<ApiResponse<AiSettings>>(
      "/v1/admin/settings/ai"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function saveAiSettings(
  request: SaveAiSettingsRequest
): Promise<AiSettings> {
  try {
    const res = await apiClient.put<ApiResponse<AiSettings>>(
      "/v1/admin/settings/ai",
      request
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function testAiConnection(
  request: TestAiConnectionRequest
): Promise<TestAiConnectionResult> {
  try {
    const res = await apiClient.post<ApiResponse<TestAiConnectionResult>>(
      "/v1/admin/settings/ai/test",
      request
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
