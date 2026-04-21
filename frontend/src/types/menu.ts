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
