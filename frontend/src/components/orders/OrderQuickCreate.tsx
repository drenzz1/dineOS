"use client";

import { useMemo, useState } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Skeleton } from "@/components/ui/Skeleton";
import { createOrder, getMenuItems } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { orderSchema } from "@/lib/validations/order";
import type { OrderFormValues } from "@/lib/validations/order";
import { useTenant } from "@/hooks/useTenant";
import type { MenuItem } from "@/types";

function formatCategory(category: string): string {
  return category.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function money(value: number): string {
  return `$${value.toFixed(2)}`;
}

export default function OrderQuickCreate() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [query, setQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("All");
  const [payAfterCreate, setPayAfterCreate] = useState(false);

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

  const orderType = useWatch({ control: form.control, name: "orderType" });
  const cartItems = useWatch({ control: form.control, name: "items" }) ?? [];
  const notesValue = useWatch({ control: form.control, name: "notes" }) ?? "";

  const { data: menuItems = [], isLoading } = useQuery({
    queryKey: queryKeys.menuItems.list(tenantId),
    queryFn: getMenuItems,
  });

  const { mutate: submitOrder, isPending } = useMutation({
    mutationFn: createOrder,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.list(tenantId) });
      form.reset();
      router.push(payAfterCreate ? "/payments" : "/orders");
    },
  });

  const categories = useMemo(
    () => ["All", ...Array.from(new Set(menuItems.map((item) => item.category)))],
    [menuItems]
  );

  const filteredItems = menuItems.filter((item) => {
    const matchesCategory =
      selectedCategory === "All" || item.category === selectedCategory;
    const matchesQuery = item.name.toLowerCase().includes(query.toLowerCase());
    return matchesCategory && matchesQuery;
  });

  const subtotal = cartItems.reduce(
    (sum, item) => sum + item.quantity * item.unitPrice,
    0
  );

  function getCartIndex(menuItemId: string): number {
    return fields.findIndex((field) => field.menuItemId === menuItemId);
  }

  function addItem(item: MenuItem) {
    const index = getCartIndex(item.id);
    if (index === -1) {
      append({
        menuItemId: item.id,
        name: item.name,
        quantity: 1,
        unitPrice: item.price,
      });
      return;
    }

    const field = fields[index];
    update(index, {
      menuItemId: field.menuItemId,
      name: field.name,
      quantity: field.quantity + 1,
      unitPrice: field.unitPrice,
    });
  }

  function changeQuantity(menuItemId: string, delta: number) {
    const index = getCartIndex(menuItemId);
    if (index === -1) return;

    const field = fields[index];
    const nextQuantity = field.quantity + delta;
    if (nextQuantity <= 0) {
      remove(index);
      return;
    }

    update(index, {
      menuItemId: field.menuItemId,
      name: field.name,
      quantity: nextQuantity,
      unitPrice: field.unitPrice,
    });
  }

  function onSubmit(values: OrderFormValues) {
    submitOrder(values);
  }

  const tableError = form.formState.errors.tableNumber?.message;
  const itemsError = form.formState.errors.items?.message;

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-5 xl:grid-cols-[1fr_380px]">
      <section className="space-y-5">
        <div className="rounded-lg border border-border bg-surface p-4 shadow-sm">
          <div className="grid gap-4 lg:grid-cols-[1fr_auto] lg:items-end">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-accent">
                Cashier order entry
              </p>
              <h2 className="mt-1 text-xl font-semibold tracking-[-0.02em] text-fg">
                Build the ticket in one screen
              </h2>
              <p className="mt-1 text-[13px] text-fg-muted">
                Choose the order type, tap menu items, review the cart, then send it to the kitchen.
              </p>
            </div>

            <div className="flex rounded-md border border-border bg-bg-sunken p-1">
              {(["dine-in", "pickup"] as const).map((type) => (
                <label
                  key={type}
                  className={`cursor-pointer rounded px-3 py-1.5 text-[13px] font-semibold capitalize transition ${
                    orderType === type
                      ? "bg-surface text-fg shadow-xs"
                      : "text-fg-muted hover:text-fg"
                  }`}
                >
                  <input
                    type="radio"
                    value={type}
                    {...form.register("orderType")}
                    className="sr-only"
                  />
                  {type === "dine-in" ? "Dine-in" : "Pickup"}
                </label>
              ))}
            </div>
          </div>

          <div className="mt-4 grid gap-3 md:grid-cols-[180px_1fr]">
            <div>
              <label htmlFor="tableNumber" className="text-xs font-semibold text-fg-muted">
                Table
              </label>
              <input
                id="tableNumber"
                type="number"
                min={1}
                max={50}
                disabled={orderType !== "dine-in"}
                placeholder={orderType === "dine-in" ? "Table number" : "Not needed"}
                {...form.register("tableNumber", {
                  setValueAs: (value: string) =>
                    value === "" ? undefined : Number.parseInt(value, 10),
                })}
                className="mt-1 h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-fg outline-none transition focus:border-accent disabled:bg-surface-2 disabled:text-fg-subtle"
              />
              {tableError && <p className="mt-1 text-xs text-danger">{tableError}</p>}
            </div>

            <div>
              <label htmlFor="menu-search" className="text-xs font-semibold text-fg-muted">
                Search menu
              </label>
              <input
                id="menu-search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search pizza, drinks, desserts..."
                className="mt-1 h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-fg outline-none transition placeholder:text-fg-subtle focus:border-accent"
              />
            </div>
          </div>
        </div>

        <div className="flex gap-2 overflow-x-auto pb-1">
          {categories.map((category) => (
            <button
              key={category}
              type="button"
              onClick={() => setSelectedCategory(category)}
              className={`shrink-0 rounded-full border px-3 py-1.5 text-xs font-semibold transition ${
                selectedCategory === category
                  ? "border-accent bg-accent text-accent-fg"
                  : "border-border bg-surface text-fg-muted hover:border-border-strong hover:text-fg"
              }`}
            >
              {category === "All" ? "All" : formatCategory(category)}
            </button>
          ))}
        </div>

        {itemsError && (
          <div className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-3 py-2 text-sm font-medium text-status-cancelled-fg">
            {itemsError}
          </div>
        )}

        {isLoading ? (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {[0, 1, 2, 3, 4, 5].map((index) => (
              <div key={index} className="rounded-lg border border-border bg-surface p-4">
                <Skeleton className="h-4 w-28" />
                <Skeleton className="mt-3 h-3 w-16" />
                <Skeleton className="mt-5 h-8 w-full" />
              </div>
            ))}
          </div>
        ) : filteredItems.length === 0 ? (
          <EmptyState
            title="No menu items found"
            description="Try another search term or switch category."
            compact
            className="rounded-lg border border-border bg-surface"
          />
        ) : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {filteredItems.map((item) => {
              const cartIndex = getCartIndex(item.id);
              const quantity = cartIndex === -1 ? 0 : fields[cartIndex].quantity;

              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => addItem(item)}
                  className="group rounded-lg border border-border bg-surface p-4 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-border-strong hover:shadow-md"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h3 className="text-sm font-semibold text-fg">{item.name}</h3>
                      <p className="mt-1 text-xs text-fg-subtle">{formatCategory(item.category)}</p>
                    </div>
                    <span className="font-mono text-sm font-semibold text-fg">
                      {money(item.price)}
                    </span>
                  </div>
                  <div className="mt-4 flex items-center justify-between">
                    <span className="text-xs font-semibold text-accent group-hover:text-accent-hover">
                      Tap to add
                    </span>
                    {quantity > 0 && (
                      <span className="rounded-full bg-accent-soft px-2 py-0.5 text-xs font-bold text-accent">
                        x{quantity}
                      </span>
                    )}
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </section>

      <aside className="xl:sticky xl:top-20 xl:self-start">
        <div className="rounded-lg border border-border bg-surface shadow-sm">
          <div className="border-b border-border px-4 py-3">
            <h2 className="text-base font-semibold tracking-[-0.01em] text-fg">
              Current ticket
            </h2>
            <p className="text-[12px] text-fg-muted">
              {cartItems.length} item{cartItems.length === 1 ? "" : "s"} selected
            </p>
          </div>

          <div className="max-h-[420px] overflow-y-auto p-4">
            {cartItems.length === 0 ? (
              <EmptyState
                title="Cart is empty"
                description="Tap menu items to build the ticket."
                compact
              />
            ) : (
              <div className="space-y-3">
                {cartItems.map((item) => (
                  <div key={item.menuItemId} className="rounded-md border border-border bg-bg-sunken p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="text-sm font-semibold text-fg">{item.name}</p>
                        <p className="text-xs text-fg-subtle">{money(item.unitPrice)} each</p>
                      </div>
                      <p className="font-mono text-sm font-semibold text-fg">
                        {money(item.unitPrice * item.quantity)}
                      </p>
                    </div>
                    <div className="mt-3 flex items-center gap-2">
                      <button
                        type="button"
                        aria-label={`Decrease quantity of ${item.name}`}
                        onClick={() => changeQuantity(item.menuItemId, -1)}
                        className="flex h-7 w-7 items-center justify-center rounded border border-border bg-surface text-sm font-semibold text-fg hover:bg-surface-2"
                      >
                        -
                      </button>
                      <span className="w-8 text-center font-mono text-sm font-semibold">
                        {item.quantity}
                      </span>
                      <button
                        type="button"
                        aria-label={`Increase quantity of ${item.name}`}
                        onClick={() => changeQuantity(item.menuItemId, 1)}
                        className="flex h-7 w-7 items-center justify-center rounded border border-border bg-surface text-sm font-semibold text-fg hover:bg-surface-2"
                      >
                        +
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="border-t border-border p-4">
            <label htmlFor="order-notes" className="text-xs font-semibold text-fg-muted">
              Notes
            </label>
            <textarea
              id="order-notes"
              rows={3}
              maxLength={300}
              placeholder="Allergies, modifiers, guest requests..."
              {...form.register("notes")}
              className="mt-1 w-full resize-none rounded-md border border-border bg-surface px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
            />
            <p className="mt-1 text-right text-[11px] text-fg-subtle">
              {notesValue.length}/300
            </p>

            <div className="mt-4 rounded-md bg-bg-sunken p-3">
              <div className="flex items-center justify-between text-sm">
                <span className="text-fg-muted">Subtotal</span>
                <span className="font-mono font-semibold">{money(subtotal)}</span>
              </div>
            </div>

            <label className="mt-4 flex cursor-pointer items-start gap-2 rounded-md border border-border bg-surface-2 p-3">
              <input
                type="checkbox"
                checked={payAfterCreate}
                onChange={(event) => setPayAfterCreate(event.target.checked)}
                className="mt-0.5 accent-[var(--accent)]"
              />
              <span>
                <span className="block text-sm font-semibold text-fg">Go to payment after saving</span>
                <span className="text-xs text-fg-muted">Useful for pickup or pay-at-counter orders.</span>
              </span>
            </label>

            <div className="mt-4 grid grid-cols-2 gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={() => router.push("/orders")}
              >
                Cancel
              </Button>
              <Button type="submit" isLoading={isPending}>
                Send order
              </Button>
            </div>
          </div>
        </div>
      </aside>
    </form>
  );
}
