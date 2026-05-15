import { z } from "zod";

export const restaurantProfileSchema = z.object({
  name: z.string().min(1, "Restaurant name is required").max(200, "Max 200 characters"),
  ownerName: z.string().min(1, "Owner name is required").max(100, "Max 100 characters"),
  phone: z.string().min(1, "Phone is required").max(30, "Max 30 characters"),
  city: z.string().min(1, "City is required").max(100, "Max 100 characters"),
});

export type RestaurantProfileFormValues = z.infer<typeof restaurantProfileSchema>;
