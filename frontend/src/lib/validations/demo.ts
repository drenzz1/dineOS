import { z } from "zod";

export const demoRequestSchema = z.object({
  email: z
    .string()
    .min(1, "Email is required")
    .email("Enter a valid email address")
    .max(254),
  acceptedTerms: z
    .boolean()
    .refine((v) => v === true, "You must accept the demo terms to continue."),
  // Honeypot — must remain empty. We do not surface a validation error to
  // legitimate users; the schema just enforces the contract.
  companyName: z.string().max(120).optional(),
});

export type DemoRequestFormValues = z.infer<typeof demoRequestSchema>;
