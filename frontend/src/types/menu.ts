export enum MenuCategory {
  Starters = "Starters",
  MainCourse = "MainCourse",
  Desserts = "Desserts",
  Drinks = "Drinks",
  Sides = "Sides",
}

export interface MenuItem {
  id: string;
  name: string;
  price: number;
  category: MenuCategory;
  description?: string;
  imageUrl?: string;
}
