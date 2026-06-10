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
  "block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500";

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
        className="inline-flex items-center gap-1 text-[13px] text-zinc-500 hover:text-zinc-900 transition-colors"
      >
        ← Back to restaurants
      </Link>

    <form
      onSubmit={handleSubmit((d) => mutate(d))}
      noValidate
      className="space-y-6"
    >
      <h1 className="text-2xl font-semibold text-zinc-900">
        Onboard Restaurant
      </h1>

      {/* Name */}
      <div className="space-y-1">
        <label htmlFor="r-name" className="block text-sm font-medium text-zinc-700">
          Restaurant name
        </label>
        <input id="r-name" type="text" {...register("name")} className={INPUT} />
        {errors.name && (
          <p className="text-sm text-red-600">{errors.name.message}</p>
        )}
      </div>

      {/* Owner name */}
      <div className="space-y-1">
        <label htmlFor="r-owner-name" className="block text-sm font-medium text-zinc-700">
          Owner name
        </label>
        <input
          id="r-owner-name"
          type="text"
          {...register("ownerName")}
          className={INPUT}
        />
        {errors.ownerName && (
          <p className="text-sm text-red-600">{errors.ownerName.message}</p>
        )}
      </div>

      {/* Owner email */}
      <div className="space-y-1">
        <label htmlFor="r-owner-email" className="block text-sm font-medium text-zinc-700">
          Owner email
        </label>
        <input
          id="r-owner-email"
          type="email"
          {...register("ownerEmail")}
          className={INPUT}
        />
        {errors.ownerEmail && (
          <p className="text-sm text-red-600">{errors.ownerEmail.message}</p>
        )}
      </div>

      {/* Phone */}
      <div className="space-y-1">
        <label htmlFor="r-phone" className="block text-sm font-medium text-zinc-700">
          Phone
        </label>
        <input id="r-phone" type="tel" {...register("phone")} className={INPUT} />
        {errors.phone && (
          <p className="text-sm text-red-600">{errors.phone.message}</p>
        )}
      </div>

      {/* City */}
      <div className="space-y-1">
        <label htmlFor="r-city" className="block text-sm font-medium text-zinc-700">
          City
        </label>
        <input id="r-city" type="text" {...register("city")} className={INPUT} />
        {errors.city && (
          <p className="text-sm text-red-600">{errors.city.message}</p>
        )}
      </div>

      {/* Plan */}
      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <span className="block text-sm font-medium text-zinc-700">Plan</span>
          <div className="group relative">
            <button
              type="button"
              className="flex h-5 w-5 items-center justify-center rounded-full bg-zinc-200 text-xs font-semibold text-zinc-600 hover:bg-zinc-300"
              aria-label="Plan feature comparison"
            >
              ?
            </button>
            <div className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 w-64 -translate-x-1/2 rounded-lg border border-zinc-200 bg-white p-3 opacity-0 shadow-lg transition-opacity group-hover:opacity-100">
              <div className="grid grid-cols-2 gap-3">
                {(["Free", "Pro"] as const).map((plan) => (
                  <div key={plan}>
                    <p className="mb-1.5 text-xs font-semibold text-zinc-900">
                      {plan}
                    </p>
                    <ul className="space-y-1">
                      {PLAN_FEATURES[plan].map((f) => (
                        <li
                          key={f}
                          className="flex items-start gap-1 text-xs text-zinc-600"
                        >
                          <span className="mt-px text-green-500">✓</span>
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
              <span className="text-sm text-zinc-800">{plan}</span>
            </label>
          ))}
        </div>
        {errors.plan && (
          <p className="text-sm text-red-600">{errors.plan.message}</p>
        )}
        <p className="rounded-md bg-zinc-50 px-3 py-2 text-[11px] text-zinc-600">
          Selecting <strong>Pro</strong> here provisions the plan without
          charging. The owner subscribes via Stripe in <em>Settings → Billing</em>{" "}
          once they sign in.
        </p>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-end gap-3 border-t border-zinc-200 pt-4">
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
