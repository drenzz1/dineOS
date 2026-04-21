"use client";

import { useState } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import type {
  FieldError,
  FieldErrors,
  FieldArrayWithId,
  UseFieldArrayAppend,
  UseFieldArrayRemove,
  UseFieldArrayUpdate,
  UseFormRegister,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { orderSchema } from "@/lib/validations/order";
import type { OrderFormValues } from "@/lib/validations/order";
import { createOrder, getMenuItems } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { useOrderWizardStore } from "@/stores/orderWizardStore";
import { Button } from "@/components/ui/Button";
import type { MenuItem } from "@/types";

// ─── Step 1 ─────────────────────────────────────────────────────────────────

interface Step1Props {
  register: UseFormRegister<OrderFormValues>;
  errors: FieldErrors<OrderFormValues>;
  watchOrderType: "dine-in" | "pickup";
}

function Step1({ register, errors, watchOrderType }: Step1Props) {
  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-zinc-900">
        Step 1 — Order type
      </h2>

      <fieldset className="space-y-3">
        <legend className="text-sm font-medium text-zinc-700">
          Order type
        </legend>
        <div className="flex gap-6">
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="radio"
              value="dine-in"
              {...register("orderType")}
              className="accent-blue-600"
            />
            <span className="text-sm text-zinc-800">Dine-in</span>
          </label>
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="radio"
              value="pickup"
              {...register("orderType")}
              className="accent-blue-600"
            />
            <span className="text-sm text-zinc-800">Pickup</span>
          </label>
        </div>
        {errors.orderType && (
          <p className="text-sm text-red-600">{errors.orderType.message}</p>
        )}
      </fieldset>

      {watchOrderType === "dine-in" && (
        <div className="space-y-1">
          <label
            htmlFor="tableNumber"
            className="block text-sm font-medium text-zinc-700"
          >
            Table number{" "}
            <span className="text-zinc-400 font-normal">(1–50)</span>
          </label>
          <input
            id="tableNumber"
            type="number"
            min={1}
            max={50}
            {...register("tableNumber", {
              setValueAs: (v: string) =>
                v === "" ? undefined : parseInt(v, 10),
            })}
            placeholder="e.g. 5"
            className="block w-32 rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 placeholder:text-zinc-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          {errors.tableNumber && (
            <p className="text-sm text-red-600">{errors.tableNumber.message}</p>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Step 2 ─────────────────────────────────────────────────────────────────

interface Step2Props {
  menuItems: MenuItem[];
  isLoading: boolean;
  fields: FieldArrayWithId<OrderFormValues, "items", "id">[];
  append: UseFieldArrayAppend<OrderFormValues, "items">;
  remove: UseFieldArrayRemove;
  update: UseFieldArrayUpdate<OrderFormValues, "items">;
  errors: FieldErrors<OrderFormValues>;
}

function Step2({
  menuItems,
  isLoading,
  fields,
  append,
  remove,
  update,
  errors,
}: Step2Props) {
  function getFieldIndex(menuItemId: string): number {
    return fields.findIndex((f) => f.menuItemId === menuItemId);
  }

  function toggleItem(item: MenuItem) {
    const idx = getFieldIndex(item.id);
    if (idx === -1) {
      append({
        menuItemId: item.id,
        name: item.name,
        quantity: 1,
        unitPrice: item.price,
      });
    } else {
      remove(idx);
    }
  }

  function changeQuantity(menuItemId: string, delta: number) {
    const idx = getFieldIndex(menuItemId);
    if (idx === -1) return;
    const field = fields[idx];
    update(idx, {
      menuItemId: field.menuItemId,
      name: field.name,
      quantity: Math.max(1, field.quantity + delta),
      unitPrice: field.unitPrice,
    });
  }

  const itemsError = (errors.items as FieldError | undefined)?.message;

  const categories = [...new Set(menuItems.map((m) => m.category))];

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-zinc-900">
        Step 2 — Select items
      </h2>

      {itemsError && (
        <p className="text-sm text-red-600">{itemsError}</p>
      )}

      {isLoading ? (
        <p className="text-sm text-zinc-500">Loading menu items...</p>
      ) : (
        <div className="space-y-5">
          {categories.map((category) => (
            <div key={category}>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-400">
                {category}
              </p>
              <div className="space-y-2">
                {menuItems
                  .filter((m) => m.category === category)
                  .map((item) => {
                    const idx = getFieldIndex(item.id);
                    const selected = idx !== -1;
                    return (
                      <div
                        key={item.id}
                        className={`flex items-center justify-between rounded-lg border p-3 transition-colors ${
                          selected
                            ? "border-blue-300 bg-blue-50"
                            : "border-zinc-200 bg-white"
                        }`}
                      >
                        <label className="flex flex-1 cursor-pointer items-center gap-3">
                          <input
                            type="checkbox"
                            checked={selected}
                            onChange={() => toggleItem(item)}
                            className="h-4 w-4 accent-blue-600"
                          />
                          <div>
                            <p className="text-sm font-medium text-zinc-900">
                              {item.name}
                            </p>
                            <p className="text-xs text-zinc-500">
                              ${item.price.toFixed(2)}
                            </p>
                          </div>
                        </label>

                        {selected && (
                          <div className="flex items-center gap-2">
                            <button
                              type="button"
                              onClick={() => changeQuantity(item.id, -1)}
                              className="flex h-7 w-7 items-center justify-center rounded-md border border-zinc-200 bg-white text-sm font-medium text-zinc-700 hover:bg-zinc-50"
                            >
                              −
                            </button>
                            <span className="w-6 text-center text-sm font-medium text-zinc-900">
                              {fields[idx].quantity}
                            </span>
                            <button
                              type="button"
                              onClick={() => changeQuantity(item.id, 1)}
                              className="flex h-7 w-7 items-center justify-center rounded-md border border-zinc-200 bg-white text-sm font-medium text-zinc-700 hover:bg-zinc-50"
                            >
                              +
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Step 3 ─────────────────────────────────────────────────────────────────

interface Step3Props {
  values: OrderFormValues;
  register: UseFormRegister<OrderFormValues>;
  errors: FieldErrors<OrderFormValues>;
}

function Step3({ values, register, errors }: Step3Props) {
  const total = values.items.reduce(
    (sum, item) => sum + item.unitPrice * item.quantity,
    0
  );

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-zinc-900">
        Step 3 — Review &amp; confirm
      </h2>

      <div className="space-y-3 rounded-lg border border-zinc-200 bg-zinc-50 p-4">
        <div className="flex items-center justify-between text-sm">
          <span className="text-zinc-500">Order type</span>
          <span className="font-medium capitalize">{values.orderType}</span>
        </div>
        {values.orderType === "dine-in" && values.tableNumber != null && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-zinc-500">Table</span>
            <span className="font-medium">{values.tableNumber}</span>
          </div>
        )}
        <div className="border-t border-zinc-200 pt-3 space-y-2">
          <p className="text-sm font-medium text-zinc-700">Items</p>
          {values.items.map((item, i) => (
            <div
              key={i}
              className="flex items-center justify-between text-sm"
            >
              <span className="text-zinc-700">
                {item.name} × {item.quantity}
              </span>
              <span className="text-zinc-500">
                ${(item.unitPrice * item.quantity).toFixed(2)}
              </span>
            </div>
          ))}
          <div className="flex items-center justify-between border-t border-zinc-200 pt-2 text-sm font-semibold">
            <span>Total</span>
            <span>${total.toFixed(2)}</span>
          </div>
        </div>
      </div>

      <div className="space-y-1">
        <label
          htmlFor="notes"
          className="block text-sm font-medium text-zinc-700"
        >
          Order notes{" "}
          <span className="text-zinc-400 font-normal">(optional)</span>
        </label>
        <textarea
          id="notes"
          rows={3}
          {...register("notes")}
          placeholder="Any special requests..."
          className="block w-full resize-none rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-900 placeholder:text-zinc-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {errors.notes && (
          <p className="text-sm text-red-600">{errors.notes.message}</p>
        )}
        <p className="text-xs text-zinc-400">Max 300 characters</p>
      </div>
    </div>
  );
}

// ─── Wizard ──────────────────────────────────────────────────────────────────

export default function OrderWizard() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const { step, nextStep, prevStep, reset: resetStep } = useOrderWizardStore();
  const [toast, setToast] = useState<{
    message: string;
    ok: boolean;
  } | null>(null);

  const form = useForm<OrderFormValues>({
    resolver: zodResolver(orderSchema),
    defaultValues: {
      orderType: "dine-in",
      items: [],
      notes: "",
    },
  });

  const { fields, append, remove, update } = useFieldArray({
    control: form.control,
    name: "items",
  });

  const watchOrderType = form.watch("orderType");

  const { data: menuItems = [], isLoading: menuLoading } = useQuery({
    queryKey: queryKeys.menuItems.list(tenantId),
    queryFn: getMenuItems,
  });

  const { mutate: submitOrder, isPending } = useMutation({
    mutationFn: createOrder,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.list(tenantId) });
      setToast({ message: "Order created successfully!", ok: true });
      resetStep();
      form.reset();
      setTimeout(() => router.push("/orders"), 1200);
    },
    onError: () => {
      setToast({
        message: "Failed to create order. Please try again.",
        ok: false,
      });
    },
  });

  async function handleNext() {
    const valid =
      step === 1
        ? await form.trigger(["orderType", "tableNumber"])
        : await form.trigger(["items"]);
    if (valid) nextStep();
  }

  function onSubmit(data: OrderFormValues) {
    submitOrder(data);
  }

  const STEP_LABELS = ["Order type", "Item selection", "Review & confirm"];

  return (
    <div className="mx-auto max-w-2xl">
      {toast && (
        <div
          className={`mb-6 rounded-md border px-4 py-3 text-sm font-medium ${
            toast.ok
              ? "border-green-200 bg-green-50 text-green-800"
              : "border-red-200 bg-red-50 text-red-800"
          }`}
        >
          {toast.message}
        </div>
      )}

      {/* Step indicator */}
      <div className="mb-8 flex items-center gap-1">
        {([1, 2, 3] as const).map((s, i) => (
          <div key={s} className="flex items-center gap-1">
            <div
              className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium ${
                s === step
                  ? "bg-blue-600 text-white"
                  : s < step
                    ? "bg-blue-100 text-blue-700"
                    : "bg-zinc-100 text-zinc-400"
              }`}
            >
              {s}
            </div>
            <span
              className={`hidden text-xs md:inline ${
                s === step ? "font-medium text-zinc-800" : "text-zinc-400"
              }`}
            >
              {STEP_LABELS[i]}
            </span>
            {s < 3 && (
              <div
                className={`mx-2 h-px w-8 ${s < step ? "bg-blue-300" : "bg-zinc-200"}`}
              />
            )}
          </div>
        ))}
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} noValidate>
        {step === 1 && (
          <Step1
            register={form.register}
            errors={form.formState.errors}
            watchOrderType={watchOrderType}
          />
        )}
        {step === 2 && (
          <Step2
            menuItems={menuItems}
            isLoading={menuLoading}
            fields={fields}
            append={append}
            remove={remove}
            update={update}
            errors={form.formState.errors}
          />
        )}
        {step === 3 && (
          <Step3
            values={form.getValues()}
            register={form.register}
            errors={form.formState.errors}
          />
        )}

        <div className="mt-8 flex items-center justify-between border-t border-zinc-200 pt-6">
          {step > 1 ? (
            <Button type="button" variant="secondary" onClick={prevStep}>
              Back
            </Button>
          ) : (
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                resetStep();
                router.push("/orders");
              }}
            >
              Cancel
            </Button>
          )}

          {step < 3 ? (
            <Button type="button" onClick={handleNext}>
              Next
            </Button>
          ) : (
            <Button type="submit" isLoading={isPending}>
              Place Order
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
