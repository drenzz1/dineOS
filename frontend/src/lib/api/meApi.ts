import apiClient from "@/lib/api/apiClient";
import type { MeResponse } from "@/types/me";

export async function getMe(): Promise<MeResponse> {
  const res = await apiClient.get<{ data: MeResponse }>("/v1/me");
  return res.data.data;
}
