export enum MenuCategory {
  Starters = "Starters",
  MainCourse = "MainCourse",
  Desserts = "Desserts",
  Drinks = "Drinks",
  Sides = "Sides",
}

export interface MenuItem {
  id: string;
  tenantId?: string;
  name: string;
  price: number;
  category: string;
  description?: string;
  imageUrl?: string;
}

// ─── Backend DTOs ─────────────────────────────────────────────────────────────

export type MenuItemDto = {
  id: number;
  tenantId: number;
  name: string;
  price: number;
  category: string;
  description?: string | null;
  imageUrl?: string | null;
};

export type MenuCategoryDto = {
  id: number;
  tenantId: number;
  name: string;
};

export type AiSuggestionMetadata = {
  model: string;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
};

export type MenuItemDescriptionSuggestion = {
  menuItemId: number;
  itemName: string;
  category: string;
  suggestedDescription: string;
  suggestedAllergens: string[];
  metadata: AiSuggestionMetadata;
};
