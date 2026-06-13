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

export type AiProvider = "Anthropic" | "OpenAI" | "Google";

export type AiSettings = {
  activeProvider: AiProvider;
  anthropicApiKeyHint: string | null;
  openAiApiKeyHint: string | null;
  googleAiApiKeyHint: string | null;
  updatedAt: string | null;
};

export type SaveAiSettingsRequest = {
  provider: AiProvider;
  apiKey: string;
};

export type TestAiConnectionRequest = {
  provider: AiProvider;
  apiKey: string;
};

export type TestAiConnectionResult = {
  success: boolean;
  error: string | null;
  model: string | null;
};
