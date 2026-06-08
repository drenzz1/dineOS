export type RestaurantPlan = "Free" | "Pro";
export type RestaurantStatus = "Active" | "Suspended";

export interface Restaurant {
  id: number;
  name: string;
  ownerName: string;
  ownerEmail: string;
  phone: string;
  plan: RestaurantPlan;
  status: RestaurantStatus;
  city: string;
  totalOrders: number;
  staffCount: number;
  revenue: number;
  createdAt: string;
  ownerEmailVerified: boolean;
}
