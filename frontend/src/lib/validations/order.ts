import { z } from "zod";

const orderItemSchema = z.object({
  menuItemId: z.string().min(1),
  name: z.string().min(1),
  quantity: z.number().int().min(1),
  unitPrice: z.number().min(0),
});

export const orderSchema = z
  .object({
    orderType: z.enum(["dine-in", "pickup"]),
    tableNumber: z.number().int().min(1).optional(),
    items: z.array(orderItemSchema).min(1, "At least 1 item required"),
    notes: z.string().max(300).optional(),
  })
  .superRefine((data, ctx) => {
    if (
      data.orderType === "dine-in" &&
      (data.tableNumber === undefined || data.tableNumber < 1)
    ) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Table number is required for dine-in orders",
        path: ["tableNumber"],
      });
    }
  });

export type OrderFormValues = z.infer<typeof orderSchema>;
