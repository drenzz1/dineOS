import { z } from "zod";

export const shiftNoteSchema = z.object({
  title: z.string().min(1, "Title is required").max(80, "Max 80 characters"),
  body: z.string().min(1, "Body is required").max(1000, "Max 1000 characters"),
  priority: z.enum(["info", "warning", "urgent"]).optional(),
});

export type ShiftNoteFormValues = z.infer<typeof shiftNoteSchema>;
