"use client";

import { useState, useEffect, useRef } from "react";
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
import RestaurantTableSelect from "./RestaurantTableSelect";

// ─── Step 1 ─────────────────────────────────────────────────────────────────

interface Step1Props {
  register: UseFormRegister<OrderFormValues>;
  errors: FieldErrors<OrderFormValues>;
  watchOrderType: "dine-in" | "pickup";
}

function Step1({ register, errors, watchOrderType }: Step1Props) {
  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-fg">
        Step 1 — Order type
      </h2>

      <fieldset className="space-y-3">
        <legend className="text-sm font-medium text-fg-muted">
          Order type
        </legend>
        <div className="flex gap-6">
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="radio"
              value="dine-in"
              data-testid="order-type-dinein"
              {...register("orderType")}
              className="accent-blue-600"
            />
            <span className="text-sm text-fg">Dine-in</span>
          </label>
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="radio"
              value="pickup"
              data-testid="order-type-pickup"
              {...register("orderType")}
              className="accent-blue-600"
            />
            <span className="text-sm text-fg">Pickup</span>
          </label>
        </div>
        {errors.orderType && (
          <p className="text-sm text-danger">{errors.orderType.message}</p>
        )}
      </fieldset>

      {watchOrderType === "dine-in" && (
        <div className="max-w-sm">
          <RestaurantTableSelect
            label="Table number"
            registration={register("tableNumber", {
              setValueAs: (v: string) =>
                v === "" ? undefined : parseInt(v, 10),
            })}
            error={errors.tableNumber?.message}
          />
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
      <h2 className="text-lg font-semibold text-fg">
        Step 2 — Select items
      </h2>

      {itemsError && (
        <p className="text-sm text-danger">{itemsError}</p>
      )}

      {isLoading ? (
        <p className="text-sm text-fg-subtle">Loading menu items...</p>
      ) : (
        <div className="space-y-5">
          {categories.map((category) => (
            <div key={category}>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-fg-muted">
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
                        data-testid="menu-item-card"
                        data-item-id={item.id}
                        className={`flex items-center justify-between rounded-lg border p-3 transition-colors ${
                          selected
                            ? "border-accent bg-accent-soft"
                            : "border-border bg-surface"
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
                            <p className="text-sm font-medium text-fg">
                              {item.name}
                            </p>
                            <p className="text-xs text-fg-subtle">
                              ${item.price.toFixed(2)}
                            </p>
                          </div>
                        </label>

                        {selected && (
                          <div className="flex items-center gap-2">
                            <button
                              type="button"
                              data-testid="menu-item-qty-decrease"
                              aria-label={`Decrease quantity of ${item.name}`}
                              onClick={() => changeQuantity(item.id, -1)}
                              className="flex h-7 w-7 items-center justify-center rounded-md border border-border bg-surface text-sm font-medium text-fg-muted hover:bg-surface-2"
                            >
                              −
                            </button>
                            <span className="w-6 text-center text-sm font-medium text-fg">
                              {fields[idx].quantity}
                            </span>
                            <button
                              type="button"
                              data-testid="menu-item-qty-increase"
                              aria-label={`Increase quantity of ${item.name}`}
                              onClick={() => changeQuantity(item.id, 1)}
                              className="flex h-7 w-7 items-center justify-center rounded-md border border-border bg-surface text-sm font-medium text-fg-muted hover:bg-surface-2"
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
  notesRef: React.RefObject<HTMLTextAreaElement | null>;
  errors: FieldErrors<OrderFormValues>;
}

function Step3({ values, notesRef, errors }: Step3Props) {
  const total = values.items.reduce(
    (sum, item) => sum + item.unitPrice * item.quantity,
    0
  );

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-fg">
        Step 3 — Review &amp; confirm
      </h2>

      <div className="space-y-3 rounded-lg border border-border bg-surface-2 p-4">
        <div className="flex items-center justify-between text-sm">
          <span className="text-fg-subtle">Order type</span>
          <span className="font-medium capitalize">{values.orderType}</span>
        </div>
        {values.orderType === "dine-in" && values.tableNumber != null && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-fg-subtle">Table</span>
            <span className="font-medium">{values.tableNumber}</span>
          </div>
        )}
        <div className="border-t border-border pt-3 space-y-2">
          <p className="text-sm font-medium text-fg-muted">Items</p>
          {values.items.map((item, i) => (
            <div
              key={i}
              className="flex items-center justify-between text-sm"
            >
              <span className="text-fg-muted">
                {item.name} × {item.quantity}
              </span>
              <span className="text-fg-subtle">
                ${(item.unitPrice * item.quantity).toFixed(2)}
              </span>
            </div>
          ))}
          <div className="flex items-center justify-between border-t border-border pt-2 text-sm font-semibold">
            <span>Total</span>
            <span>${total.toFixed(2)}</span>
          </div>
        </div>
      </div>

      <div className="space-y-1">
        <label
          htmlFor="notes"
          className="block text-sm font-medium text-fg-muted"
        >
          Order notes{" "}
          <span className="text-fg-muted font-normal">(optional)</span>
        </label>
        {/* Uncontrolled textarea — value is read via ref at submit time.
            The __e2e:set-order-note event sets notesRef.current.value directly,
            bypassing React 19 synthetic event issues in Playwright. */}
        <textarea
          id="notes"
          rows={3}
          data-testid="order-note-input"
          name="notes"
          ref={notesRef}
          defaultValue=""
          placeholder="Any special requests..."
          className="block w-full resize-none rounded-md border border-border-strong bg-surface px-3 py-2 text-sm text-fg placeholder:text-fg-subtle focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent"
        />
        {errors.notes && (
          <p className="text-sm text-danger">{errors.notes.message}</p>
        )}
        <p className="text-xs text-fg-muted">Max 300 characters</p>
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

  // Uncontrolled ref for the notes textarea — read at submit time, set directly
  // by the __e2e:set-order-note event handler. Avoids stale-closure and React 19
  // concurrent-render issues that defeated every state-based approach.
  const notesInputRef = useRef<HTMLTextAreaElement>(null);

  const form = useForm<OrderFormValues>({
    resolver: zodResolver(orderSchema),
    defaultValues: {
      orderType: "dine-in",
      items: [],
      notes: "",
    },
  });

  // E2E hook: Playwright cannot trigger React 19 synthetic onChange on a textarea.
  // window.__e2eNotes is used as a secondary channel alongside the DOM ref because
  // React 19 can reset the uncontrolled textarea value during reconciliation.
  useEffect(() => {
    const handler = (e: Event) => {
      const value = (e as CustomEvent<string>).detail;
      (window as Window & { __e2eNotes?: string }).__e2eNotes = value;
      if (notesInputRef.current) notesInputRef.current.value = value;
    };
    document.addEventListener("__e2e:set-order-note", handler);
    return () => document.removeEventListener("__e2e:set-order-note", handler);
  }, []);

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
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
      setToast({ message: "Order created successfully!", ok: true });
      resetStep();
      form.reset();
      if (notesInputRef.current) notesInputRef.current.value = "";
      delete (window as Window & { __e2eNotes?: string }).__e2eNotes;
      setTimeout(() => router.push("/orders"), 1200);
    },
    onError: (error) => {
      setToast({
        message:
          error instanceof Error
            ? error.message
            : "Failed to create order. Please try again.",
        ok: false,
      });
    },
  });

  async function handleNext() {
    const valid =
      step === 1
        ? await form.trigger(["orderType", "tableNumber"])
        : await form.trigger(["items"]);
    // Defer the step transition to the next animation frame so the DOM swap
    // (wizard-next → wizard-submit) happens after the current click event chain
    // completes, preventing mouseup from landing on the newly-rendered submit button.
    if (valid) requestAnimationFrame(nextStep);
  }

  function onSubmit(data: OrderFormValues) {
    const w = window as Window & { __e2eNotes?: string };
    const notes = notesInputRef.current?.value || w.__e2eNotes || data.notes;
    submitOrder({ ...data, notes });
  }

  const STEP_LABELS = ["Order type", "Item selection", "Review & confirm"];

  return (
    <div data-testid="order-wizard" className="mx-auto max-w-2xl">
      {toast && (
        <div
          data-testid={toast.ok ? "toast-success" : undefined}
          className={`mb-6 rounded-md border px-4 py-3 text-sm font-medium ${
            toast.ok
              ? "border-status-ready-border bg-status-ready-bg text-success"
              : "border-status-cancelled-border bg-status-cancelled-bg text-danger"
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
                  ? "bg-accent text-white"
                  : s < step
                    ? "bg-status-new-bg text-info"
                    : "bg-surface-2 text-fg-muted"
              }`}
            >
              {s}
            </div>
            <span
              className={`hidden text-xs md:inline ${
                s === step ? "font-medium text-fg" : "text-fg-muted"
              }`}
            >
              {STEP_LABELS[i]}
            </span>
            {s < 3 && (
              <div
                className={`mx-2 h-px w-8 ${s < step ? "bg-status-new-bg" : "bg-surface-3"}`}
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
            notesRef={notesInputRef}
            errors={form.formState.errors}
          />
        )}

        <div className="mt-8 flex items-center justify-between border-t border-border pt-6">
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
            <Button data-testid="wizard-next" type="button" onClick={handleNext}>
              Next
            </Button>
          ) : (
            <Button data-testid="wizard-submit" type="submit" isLoading={isPending}>
              Place Order
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
