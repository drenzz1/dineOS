import { z } from "zod";

export const restaurantSchema = z.object({
  name: z.string().min(1, "Restaurant name is required"),
  ownerName: z.string().min(1, "Owner name is required"),
  ownerEmail: z.string().min(1, "Email is required").email("Invalid email address"),
  phone: z.string().min(1, "Phone is required"),
  plan: z.enum(["Free", "Pro"], { error: "Plan is required" }),
  city: z.string().min(1, "City is required"),
});

export type RestaurantFormValues = z.infer<typeof restaurantSchema>;

export const emailVerificationSchema = z.object({
  code: z
    .string()
    .length(6, "Code must be exactly 6 digits")
    .regex(/^\d{6}$/, "Code must be exactly 6 digits"),
});

export type EmailVerificationFormValues = z.infer<typeof emailVerificationSchema>;
