import { z } from "zod";

export const forgotPasswordSchema = z.object({
  email: z.string().min(1, "Email is required").email("Enter a valid email"),
});

export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    email: z.string().min(1, "Email is required").email("Enter a valid email"),
    code: z.string().regex(/^\d{6}$/, "Enter the 6-digit code from the email"),
    newPassword: z
      .string()
      .min(12, "Use at least 12 characters")
      .max(200, "Max 200 characters"),
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords must match",
  });

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;
