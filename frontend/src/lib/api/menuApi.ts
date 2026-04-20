import type { MenuItem } from "@/types";
import type { MenuItemFormValues } from "@/lib/validations/menuItem";
import { MenuCategory } from "@/types";

let categories: string[] = Object.values(MenuCategory);

let mockMenuItems: MenuItem[] = [
  { id: "1", name: "Margherita Pizza", price: 12.99, category: MenuCategory.MainCourse },
  { id: "2", name: "Caesar Salad", price: 8.99, category: MenuCategory.Starters },
];

export async function getCategories(): Promise<string[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 100));
  return categories;
}

export async function addCategory(name: string): Promise<string[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 100));
  if (!categories.includes(name)) {
    categories = [...categories, name];
  }
  return categories;
}

export async function getMenuItems(): Promise<MenuItem[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockMenuItems;
}

export async function saveMenuItem(
  data: MenuItemFormValues,
  id?: string
): Promise<MenuItem> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  if (id) {
    const updated: MenuItem = {
      id,
      name: data.name,
      price: data.price,
      category: data.category,
      description: data.description,
    };
    mockMenuItems = mockMenuItems.map((m) => (m.id === id ? updated : m));
    return updated;
  }
  const created: MenuItem = {
    id: crypto.randomUUID(),
    name: data.name,
    price: data.price,
    category: data.category,
    description: data.description,
  };
  mockMenuItems = [...mockMenuItems, created];
  return created;
}

export async function deleteMenuItem(id: string): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  mockMenuItems = mockMenuItems.filter((m) => m.id !== id);
}
