import type { AdminUser } from "@/types";

const MOCK_USERS: AdminUser[] = [
  {
    id: "u1",
    name: "Alice Rossi",
    email: "alice@bellacucina.com",
    role: "Manager",
    restaurantName: "Bella Cucina",
    status: "Active",
    lastLogin: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "u2",
    name: "Bob Marku",
    email: "bob@bellacucina.com",
    role: "Cashier",
    restaurantName: "Bella Cucina",
    status: "Active",
    lastLogin: new Date(Date.now() - 25 * 60 * 1000).toISOString(),
  },
  {
    id: "u3",
    name: "Carol Duka",
    email: "carol@pastapalace.com",
    role: "KitchenStaff",
    restaurantName: "Pasta Palace",
    status: "Inactive",
    lastLogin: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "u4",
    name: "Dave Ademi",
    email: "dave@dineos.com",
    role: "SuperAdmin",
    restaurantName: null,
    status: "Active",
    lastLogin: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
  },
  {
    id: "u5",
    name: "Eve Krasniqi",
    email: "eve@pastapalace.com",
    role: "Cashier",
    restaurantName: "Pasta Palace",
    status: "Suspended",
    lastLogin: new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "u6",
    name: "Frank Hoxha",
    email: "frank@grillhouse.com",
    role: "KitchenStaff",
    restaurantName: "Grill House",
    status: "Active",
    lastLogin: null,
  },
];

export async function getAdminUsers(): Promise<AdminUser[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return MOCK_USERS;
}
