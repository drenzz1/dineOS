"use client";

import { useState, useEffect, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { getAdminUsers } from "@/lib/api/adminApi";
import { queryKeys } from "@/lib/api/queryKeys";
import UsersTable from "@/components/admin/UsersTable";
import type { Role } from "@/types";

type RoleFilter = Role | "All";

const ROLE_OPTIONS: RoleFilter[] = [
  "All",
  "Manager",
  "Cashier",
  "KitchenStaff",
  "SuperAdmin",
];

const INPUT_CLASSES =
  "block rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500";

export default function UsersPage() {
  const { data: users = [], isLoading } = useQuery({
    queryKey: queryKeys.adminUsers.list(),
    queryFn: getAdminUsers,
  });

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState<RoleFilter>("All");

  useEffect(() => {
    const id = setTimeout(() => setDebouncedSearch(search.trim()), 250);
    return () => clearTimeout(id);
  }, [search]);

  const filtered = useMemo(() => {
    return users.filter((user) => {
      if (roleFilter !== "All" && user.role !== roleFilter) return false;
      if (!debouncedSearch) return true;
      const q = debouncedSearch.toLowerCase();
      return (
        user.name.toLowerCase().includes(q) ||
        user.email.toLowerCase().includes(q)
      );
    });
  }, [users, roleFilter, debouncedSearch]);

  const isFiltering = debouncedSearch !== "" || roleFilter !== "All";

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-zinc-900">Users</h1>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <input
          type="search"
          placeholder="Search by name or email…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search users"
          className={`${INPUT_CLASSES} w-full sm:max-w-xs`}
        />
        <select
          value={roleFilter}
          onChange={(e) => setRoleFilter(e.target.value as RoleFilter)}
          aria-label="Filter by role"
          className={INPUT_CLASSES}
        >
          {ROLE_OPTIONS.map((r) => (
            <option key={r} value={r}>
              {r === "All" ? "All roles" : r}
            </option>
          ))}
        </select>
      </div>

      <UsersTable
        users={filtered}
        isLoading={isLoading}
        emptyMessage={
          isFiltering ? "No users match your filters." : "No users found."
        }
      />
    </div>
  );
}
