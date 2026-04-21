import type { Restaurant, RestaurantPlan, RestaurantStatus } from "@/types";
import type { RestaurantFormValues } from "@/lib/validations/restaurant";

function ago(days: number): string {
  return new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString();
}

let mockRestaurants: Restaurant[] = [
  {
    id: "r1",
    name: "Bella Cucina",
    ownerName: "Alice Rossi",
    ownerEmail: "alice@bellacucina.com",
    phone: "+355 69 123 4567",
    plan: "Pro",
    status: "Active",
    city: "Tirana",
    totalOrders: 1240,
    staffCount: 12,
    revenue: 28450,
    createdAt: ago(180),
  },
  {
    id: "r2",
    name: "Pasta Palace",
    ownerName: "Carol Duka",
    ownerEmail: "carol@pastapalace.com",
    phone: "+355 68 234 5678",
    plan: "Free",
    status: "Active",
    city: "Durrës",
    totalOrders: 340,
    staffCount: 4,
    revenue: 6200,
    createdAt: ago(90),
  },
  {
    id: "r3",
    name: "Grill House",
    ownerName: "Frank Hoxha",
    ownerEmail: "frank@grillhouse.com",
    phone: "+355 67 345 6789",
    plan: "Pro",
    status: "Suspended",
    city: "Vlorë",
    totalOrders: 890,
    staffCount: 8,
    revenue: 15300,
    createdAt: ago(120),
  },
  {
    id: "r4",
    name: "The Bistro",
    ownerName: "Maria Basha",
    ownerEmail: "maria@thebistro.com",
    phone: "+355 69 456 7890",
    plan: "Free",
    status: "Active",
    city: "Shkodër",
    totalOrders: 120,
    staffCount: 3,
    revenue: 2100,
    createdAt: ago(30),
  },
];

export async function getRestaurants(): Promise<Restaurant[]> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  return mockRestaurants;
}

export async function getRestaurant(id: string): Promise<Restaurant> {
  await new Promise<void>((resolve) => setTimeout(resolve, 300));
  const r = mockRestaurants.find((r) => r.id === id);
  if (!r) throw new Error(`Restaurant ${id} not found`);
  return r;
}

export async function createRestaurant(
  data: RestaurantFormValues
): Promise<Restaurant> {
  await new Promise<void>((resolve) => setTimeout(resolve, 500));
  const created: Restaurant = {
    id: crypto.randomUUID(),
    ...data,
    status: "Active",
    totalOrders: 0,
    staffCount: 0,
    revenue: 0,
    createdAt: new Date().toISOString(),
  };
  mockRestaurants = [...mockRestaurants, created];
  return created;
}

export async function updateRestaurantStatus(
  id: string,
  status: RestaurantStatus
): Promise<Restaurant> {
  await new Promise<void>((resolve) => setTimeout(resolve, 400));
  const r = mockRestaurants.find((r) => r.id === id);
  if (!r) throw new Error(`Restaurant ${id} not found`);
  const updated = { ...r, status };
  mockRestaurants = mockRestaurants.map((r) => (r.id === id ? updated : r));
  return updated;
}

export async function updateRestaurantPlan(
  id: string,
  plan: RestaurantPlan
): Promise<Restaurant> {
  await new Promise<void>((resolve) => setTimeout(resolve, 400));
  const r = mockRestaurants.find((r) => r.id === id);
  if (!r) throw new Error(`Restaurant ${id} not found`);
  const updated = { ...r, plan };
  mockRestaurants = mockRestaurants.map((r) => (r.id === id ? updated : r));
  return updated;
}
