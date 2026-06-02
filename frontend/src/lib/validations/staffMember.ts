import { z } from "zod";

const pinSchema = z
  .string()
  .regex(/^\d{4}$/, "PIN must be exactly 4 digits");

// Create: a 4-digit login PIN is mandatory — the backend's
// CreateStaffMemberRequest requires it. The PIN is what a staff member types to
// start a role-scoped staff session on a shared terminal (#staff-pin-auth).
export const staffMemberSchema = z.object({
  fullName: z.string().min(1, "Full name is required"),
  email: z.string().min(1, "Email is required").email("Invalid email"),
  role: z.enum(["Manager", "Cashier", "KitchenStaff"], {
    error: "Role is required",
  }),
  pin: pinSchema,
});

// Edit: the PIN is optional — leaving it blank keeps the existing PIN. The
// backend's UpdateStaffMemberRequest validates the PIN only when one is sent.
export const editStaffMemberSchema = staffMemberSchema.extend({
  pin: z.union([pinSchema, z.literal("")]).optional(),
});

export type StaffMemberFormValues = z.infer<typeof staffMemberSchema>;
export type EditStaffMemberFormValues = z.infer<typeof editStaffMemberSchema>;
