import { z } from "zod";

export const createRestaurantTableSchema = z.object({
  number: z
    .number({ error: "Table number is required" })
    .int("Must be a whole number")
    .gt(0, "Must be greater than 0"),
  capacity: z
    .number({ error: "Capacity is required" })
    .int("Must be a whole number")
    .min(1, "Min 1")
    .max(50, "Max 50"),
  location: z
    .string()
    .max(100, "Max 100 characters")
    .optional()
    .or(z.literal("").transform(() => undefined)),
});

export const updateRestaurantTableSchema = z.object({
  number: z.number().int().gt(0, "Must be greater than 0").optional(),
  capacity: z.number().int().min(1).max(50, "Max 50").optional(),
  location: z.string().max(100, "Max 100 characters").nullable().optional(),
  isActive: z.boolean().optional(),
});

export type CreateRestaurantTableFormValues = z.infer<typeof createRestaurantTableSchema>;
export type UpdateRestaurantTableFormValues = z.infer<typeof updateRestaurantTableSchema>;
