"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/api/queryKeys";
import { restaurantSchema } from "@/lib/validations/restaurant";
import type { RestaurantFormValues } from "@/lib/validations/restaurant";
import { createRestaurant } from "@/lib/api/restaurantApi";
import { Button } from "@/components/ui/Button";

const INPUT =
  "block w-full rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent";

const PLAN_FEATURES: Record<"Free" | "Pro", string[]> = {
  Free: ["Up to 5 staff", "Basic order management", "Standard reports"],
  Pro: [
    "Up to 20 staff",
    "Full analytics & reports",
    "Priority support",
    "Custom menu categories",
  ],
};

export default function RestaurantOnboardForm() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RestaurantFormValues>({
    resolver: zodResolver(restaurantSchema),
    defaultValues: { plan: "Free" },
  });

  const { mutate, isPending } = useMutation({
    mutationFn: createRestaurant,
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.adminRestaurants.all });
      router.push(`/admin/restaurants/${result.id}`);
    },
  });

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <Link
        href="/admin/restaurants"
        className="inline-flex items-center gap-1 text-[13px] text-fg-subtle hover:text-fg transition-colors"
      >
        ← Back to restaurants
      </Link>

    <form
      onSubmit={handleSubmit((d) => mutate(d))}
      noValidate
      className="space-y-6"
    >
      <h1 className="text-2xl font-semibold text-fg">
        Onboard Restaurant
      </h1>

      {/* Name */}
      <div className="space-y-1">
        <label htmlFor="r-name" className="block text-sm font-medium text-fg-muted">
          Restaurant name
        </label>
        <input id="r-name" type="text" {...register("name")} className={INPUT} />
        {errors.name && (
          <p className="text-sm text-danger">{errors.name.message}</p>
        )}
      </div>

      {/* Owner name */}
      <div className="space-y-1">
        <label htmlFor="r-owner-name" className="block text-sm font-medium text-fg-muted">
          Owner name
        </label>
        <input
          id="r-owner-name"
          type="text"
          {...register("ownerName")}
          className={INPUT}
        />
        {errors.ownerName && (
          <p className="text-sm text-danger">{errors.ownerName.message}</p>
        )}
      </div>

      {/* Owner email */}
      <div className="space-y-1">
        <label htmlFor="r-owner-email" className="block text-sm font-medium text-fg-muted">
          Owner email
        </label>
        <input
          id="r-owner-email"
          type="email"
          {...register("ownerEmail")}
          className={INPUT}
        />
        {errors.ownerEmail && (
          <p className="text-sm text-danger">{errors.ownerEmail.message}</p>
        )}
      </div>

      {/* Phone */}
      <div className="space-y-1">
        <label htmlFor="r-phone" className="block text-sm font-medium text-fg-muted">
          Phone
        </label>
        <input id="r-phone" type="tel" {...register("phone")} className={INPUT} />
        {errors.phone && (
          <p className="text-sm text-danger">{errors.phone.message}</p>
        )}
      </div>

      {/* City */}
      <div className="space-y-1">
        <label htmlFor="r-city" className="block text-sm font-medium text-fg-muted">
          City
        </label>
        <input id="r-city" type="text" {...register("city")} className={INPUT} />
        {errors.city && (
          <p className="text-sm text-danger">{errors.city.message}</p>
        )}
      </div>

      {/* Plan */}
      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <span className="block text-sm font-medium text-fg-muted">Plan</span>
          <div className="group relative">
            <button
              type="button"
              className="flex h-5 w-5 items-center justify-center rounded-full bg-surface-3 text-xs font-semibold text-fg-muted hover:bg-surface-3"
              aria-label="Plan feature comparison"
            >
              ?
            </button>
            <div className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 w-64 -translate-x-1/2 rounded-lg border border-border bg-surface p-3 opacity-0 shadow-lg transition-opacity group-hover:opacity-100">
              <div className="grid grid-cols-2 gap-3">
                {(["Free", "Pro"] as const).map((plan) => (
                  <div key={plan}>
                    <p className="mb-1.5 text-xs font-semibold text-fg">
                      {plan}
                    </p>
                    <ul className="space-y-1">
                      {PLAN_FEATURES[plan].map((f) => (
                        <li
                          key={f}
                          className="flex items-start gap-1 text-xs text-fg-muted"
                        >
                          <span className="mt-px text-success">✓</span>
                          {f}
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="flex gap-6">
          {(["Free", "Pro"] as const).map((plan) => (
            <label key={plan} className="flex cursor-pointer items-center gap-2">
              <input
                type="radio"
                value={plan}
                {...register("plan")}
                className="accent-blue-600"
              />
              <span className="text-sm text-fg">{plan}</span>
            </label>
          ))}
        </div>
        {errors.plan && (
          <p className="text-sm text-danger">{errors.plan.message}</p>
        )}
        <p className="rounded-md bg-surface-2 px-3 py-2 text-[11px] text-fg-muted">
          Selecting <strong>Pro</strong> here provisions the plan without
          charging. The owner subscribes via Stripe in <em>Settings → Billing</em>{" "}
          once they sign in.
        </p>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-end gap-3 border-t border-border pt-4">
        <Button
          type="button"
          variant="secondary"
          onClick={() => router.push("/admin/restaurants")}
        >
          Cancel
        </Button>
        <Button type="submit" isLoading={isPending}>
          Onboard Restaurant
        </Button>
      </div>
    </form>
    </div>
  );
}
