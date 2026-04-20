import { z } from "zod";

export const menuItemSchema = z.object({
  name: z.string().min(1, "Name is required").max(60, "Max 60 characters"),
  price: z
    .number({ error: "Price must be a number" })
    .positive("Price must be positive"),
  category: z.string().min(1, "Category is required"),
  description: z.string().max(200, "Max 200 characters").optional(),
  imageFile: z
    .custom<File | null | undefined>((val) => val == null || val instanceof File)
    .refine(
      (f) => !f || f.size <= 2 * 1024 * 1024,
      "Image must be smaller than 2 MB"
    )
    .refine(
      (f) => !f || f.type.startsWith("image/"),
      "Only image files are allowed"
    )
    .optional(),
});

export type MenuItemFormValues = z.infer<typeof menuItemSchema>;
