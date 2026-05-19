import { z } from "zod";

export const signupSchema = z.object({
  restaurantName: z.string().min(1, "Restaurant name is required").max(200),
  ownerName: z.string().min(1, "Owner name is required").max(100),
  ownerEmail: z.string().min(1, "Email is required").email("Enter a valid email address"),
  phone: z.string().min(1, "Phone is required").max(30),
  city: z.string().min(1, "City is required").max(100),
});

export type SignupFormValues = z.infer<typeof signupSchema>;
