import { z } from "zod";

export const setPasswordSchema = z
  .object({
    newPassword: z
      .string()
      .min(12, "Password must be at least 12 characters")
      .max(128, "Password is too long")
      .regex(/[A-Z]/, "Must contain an uppercase letter")
      .regex(/[a-z]/, "Must contain a lowercase letter")
      .regex(/[0-9]/, "Must contain a digit"),
    confirmPassword: z.string().min(1, "Please confirm your password"),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords do not match",
  });

export type SetPasswordFormValues = z.infer<typeof setPasswordSchema>;
