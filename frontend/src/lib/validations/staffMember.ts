import { z } from "zod";

export const staffMemberSchema = z.object({
  fullName: z.string().min(1, "Full name is required"),
  email: z.string().min(1, "Email is required").email("Invalid email address"),
  role: z.enum(["Manager", "Cashier", "KitchenStaff"], {
    error: "Role is required",
  }),
  pin: z
    .string()
    .length(4, "PIN must be exactly 4 digits")
    .regex(/^\d{4}$/, "PIN must contain only digits"),
});

export type StaffMemberFormValues = z.infer<typeof staffMemberSchema>;
