import { z } from "zod";

export const staffMemberSchema = z.object({
  fullName: z.string().min(1, "Full name is required"),
  email: z.string().min(1, "Email is required").email("Invalid email"),
  role: z.enum(["Manager", "Cashier", "KitchenStaff"], {
    error: "Role is required",
  }),
});

export type StaffMemberFormValues = z.infer<typeof staffMemberSchema>;
