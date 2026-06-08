"use client";

import { useState, useEffect, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { getRestaurants } from "@/lib/api/restaurantApi";
import { queryKeys } from "@/lib/api/queryKeys";
import RestaurantTable from "@/components/admin/RestaurantTable";
import type { RestaurantStatus } from "@/types";

type StatusFilter = RestaurantStatus | "All";

const INPUT =
  "block rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500";

export default function AdminRestaurantsPage() {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("All");

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search.trim()), 250);
    return () => clearTimeout(id);
  }, [search]);

  // Name/email search runs server-side (the backend's `search` param); status is
  // a two-value enum, so we keep that filter on the client to avoid an extra
  // round-trip. placeholderData keeps the current rows visible while a new search
  // is in flight rather than flashing the skeleton on every keystroke.
  const { data: restaurants = [], isLoading } = useQuery({
    queryKey: queryKeys.adminRestaurants.list(debouncedSearch || null),
    queryFn: () =>
      getRestaurants({ search: debouncedSearch || undefined, pageSize: 100 }),
    placeholderData: (prev) => prev,
  });

  const filtered = useMemo(() => {
    if (statusFilter === "All") return restaurants;
    return restaurants.filter((r) => r.status === statusFilter);
  }, [restaurants, statusFilter]);

  const isFiltering = debouncedSearch !== "" || statusFilter !== "All";

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Restaurants</h1>
        <Link
          href="/admin/restaurants/new"
          className="inline-flex h-10 items-center justify-center rounded-md bg-blue-600 px-4 text-sm font-medium text-white transition-colors hover:bg-blue-700"
        >
          Add Restaurant
        </Link>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <input
          type="search"
          placeholder="Search by name or email…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search restaurants"
          className={`${INPUT} w-full sm:max-w-xs`}
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
          aria-label="Filter by status"
          className={INPUT}
        >
          <option value="All">All statuses</option>
          <option value="Active">Active</option>
          <option value="Suspended">Suspended</option>
        </select>
      </div>

      <RestaurantTable
        restaurants={filtered}
        isLoading={isLoading}
        emptyMessage={
          isFiltering
            ? "No restaurants match your filters."
            : "No restaurants found."
        }
      />
    </div>
  );
}
