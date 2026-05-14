import { z } from "zod";

export const shiftSchema = z
  .object({
    staffMemberId: z.number().int().min(1, "Please select a staff member"),
    startTime: z.string().min(1, "Start time is required"),
    endTime: z.string().min(1, "End time is required"),
    notes: z.string().max(500, "Max 500 characters").optional(),
  })
  .refine((data) => !data.startTime || !data.endTime || data.endTime > data.startTime, {
    message: "End time must be after start time",
    path: ["endTime"],
  });

export type ShiftFormValues = z.infer<typeof shiftSchema>;
