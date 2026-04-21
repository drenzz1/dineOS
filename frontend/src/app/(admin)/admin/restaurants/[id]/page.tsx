"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getRestaurant,
  updateRestaurantStatus,
  updateRestaurantPlan,
} from "@/lib/api/restaurantApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import type { RestaurantPlan } from "@/types";

const PLAN_STAFF_LIMIT: Record<RestaurantPlan, number> = {
  Free: 5,
  Pro: 20,
};

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-white p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-zinc-400">
        {label}
      </p>
      <p className="mt-1 text-xl font-bold text-zinc-900">{value}</p>
    </div>
  );
}

export default function RestaurantDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pendingPlan, setPendingPlan] = useState<RestaurantPlan | "">("");

  const { data: restaurant, isLoading } = useQuery({
    queryKey: queryKeys.adminRestaurants.detail(id),
    queryFn: () => getRestaurant(id),
  });

  const { mutate: toggleStatus, isPending: isStatusPending } = useMutation({
    mutationFn: () =>
      updateRestaurantStatus(
        id,
        restaurant!.status === "Active" ? "Suspended" : "Active"
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.adminRestaurants.all });
      setConfirmOpen(false);
    },
  });

  const { mutate: changePlan, isPending: isPlanPending } = useMutation({
    mutationFn: (plan: RestaurantPlan) => updateRestaurantPlan(id, plan),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.adminRestaurants.all });
      setPendingPlan("");
    },
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-56 animate-pulse rounded-md bg-zinc-200" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-lg bg-zinc-100" />
          ))}
        </div>
      </div>
    );
  }

  if (!restaurant) {
    return <p className="text-sm text-red-600">Restaurant not found.</p>;
  }

  const staffLimit = PLAN_STAFF_LIMIT[restaurant.plan];
  const staffPct = Math.min(100, (restaurant.staffCount / staffLimit) * 100);
  const barColor =
    staffPct >= 90
      ? "bg-red-500"
      : staffPct >= 70
        ? "bg-amber-400"
        : "bg-blue-500";

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-zinc-900">
            {restaurant.name}
          </h1>
          <p className="text-sm text-zinc-500">{restaurant.city}</p>
        </div>
        <Button
          variant={restaurant.status === "Active" ? "danger" : "primary"}
          onClick={() => setConfirmOpen(true)}
        >
          {restaurant.status === "Active" ? "Suspend" : "Reactivate"}
        </Button>
      </div>

      {/* Overview */}
      <section>
        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-zinc-400">
          Overview
        </h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          <StatCard label="Owner" value={restaurant.ownerName} />
          <StatCard label="Email" value={restaurant.ownerEmail} />
          <StatCard label="Plan" value={restaurant.plan} />
          <StatCard label="Status" value={restaurant.status} />
          <StatCard
            label="Date Joined"
            value={new Date(restaurant.createdAt).toLocaleDateString("en-GB")}
          />
          <StatCard
            label="Total Orders"
            value={restaurant.totalOrders.toLocaleString()}
          />
          <StatCard
            label="Revenue"
            value={`$${restaurant.revenue.toLocaleString()}`}
          />
        </div>
      </section>

      {/* Staff */}
      <section>
        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-zinc-400">
          Staff
        </h2>
        <div className="flex items-center gap-4 rounded-lg border border-zinc-200 bg-white p-4">
          <div className="flex-1">
            <p className="text-xs text-zinc-500">Used slots</p>
            <p className="mt-0.5 text-lg font-bold text-zinc-900">
              {restaurant.staffCount} / {staffLimit} staff
            </p>
          </div>
          <div className="h-2 w-36 overflow-hidden rounded-full bg-zinc-100">
            <div
              className={`h-full rounded-full transition-all ${barColor}`}
              style={{ width: `${staffPct}%` }}
            />
          </div>
        </div>
      </section>

      {/* Plan change */}
      <section>
        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-zinc-400">
          Plan
        </h2>
        <div className="flex items-center gap-3 rounded-lg border border-zinc-200 bg-white p-4">
          <select
            value={pendingPlan || restaurant.plan}
            onChange={(e) => setPendingPlan(e.target.value as RestaurantPlan)}
            aria-label="Change plan"
            className="rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="Free">Free</option>
            <option value="Pro">Pro</option>
          </select>
          <Button
            size="sm"
            isLoading={isPlanPending}
            disabled={!pendingPlan || pendingPlan === restaurant.plan}
            onClick={() =>
              pendingPlan && changePlan(pendingPlan as RestaurantPlan)
            }
          >
            Save Plan
          </Button>
        </div>
      </section>

      {/* Confirm dialog */}
      <Modal
        isOpen={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        title={
          restaurant.status === "Active"
            ? "Suspend Restaurant"
            : "Reactivate Restaurant"
        }
      >
        <p className="text-sm text-zinc-600">
          {restaurant.status === "Active"
            ? `Are you sure you want to suspend ${restaurant.name}? Staff will lose access immediately.`
            : `Reactivate ${restaurant.name}? Staff will regain access.`}
        </p>
        <div className="mt-5 flex justify-end gap-3">
          <Button variant="secondary" onClick={() => setConfirmOpen(false)}>
            Cancel
          </Button>
          <Button
            variant={restaurant.status === "Active" ? "danger" : "primary"}
            isLoading={isStatusPending}
            onClick={() => toggleStatus()}
          >
            {restaurant.status === "Active" ? "Suspend" : "Reactivate"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
