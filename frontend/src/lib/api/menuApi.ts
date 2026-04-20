import type { MenuItem } from "@/types";
import type { MenuItemFormValues } from "@/lib/validations/menuItem";
import { MenuCategory } from "@/types";

let mockMenuItems: MenuItem[] = [
  { id: "1", name: "Margherita Pizza", price: 12.99, category: MenuCategory.MainCourse },
  { id: "2", name: "Caesar Salad", price: 8.99, category: MenuCategory.Starters },
];

export async function saveMenuItem(
  data: MenuItemFormValues,
  id?: string
): Promise<MenuItem> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  if (id) {
    const updated: MenuItem = { id, name: data.name, price: data.price, category: data.category, description: data.description };
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

export async function getMenuItems(): Promise<MenuItem[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockMenuItems;
}
