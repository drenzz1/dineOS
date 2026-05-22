import { z } from "zod";

export const firstLoginSchema = z
  .object({
    email: z.string().email("Enter a valid email"),
    currentPassword: z.string().min(1, "Temporary password is required"),
    newPassword: z
      .string()
      .min(12, "Use at least 12 characters")
      .max(200, "Max 200 characters"),
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords must match",
  })
  .refine((v) => v.newPassword !== v.currentPassword, {
    path: ["newPassword"],
    message: "New password must differ from the temporary one",
  });

export type FirstLoginFormValues = z.infer<typeof firstLoginSchema>;
