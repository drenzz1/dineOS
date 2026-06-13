"use client";

import { useState, useEffect, useMemo, useCallback } from "react";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { getAdminUsers } from "@/lib/api/adminApi";
import { queryKeys } from "@/lib/api/queryKeys";
import UsersTable from "@/components/admin/UsersTable";
import type { Role } from "@/types";

type RoleFilter = Role | "SuperAdmin" | "All";

const ROLE_OPTIONS: RoleFilter[] = [
  "All",
  "Manager",
  "Cashier",
  "KitchenStaff",
  "SuperAdmin",
];

const INPUT_CLASSES =
  "block rounded-md border border-border-strong px-3 py-2 text-sm text-fg focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent";

export default function UsersPage() {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState<RoleFilter>("All");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);

  useEffect(() => {
    const id = setTimeout(() => {
      const trimmed = search.trim();
      setDebouncedSearch(trimmed);
      setPage(1);
    }, 250);
    return () => clearTimeout(id);
  }, [search]);

  const queryParams = {
    search: debouncedSearch || undefined,
    page,
    pageSize,
  };

  const {
    data,
    isLoading,
    isError,
    isPlaceholderData,
    refetch,
  } = useQuery({
    queryKey: queryKeys.adminUsers.list(queryParams),
    queryFn: () => getAdminUsers(queryParams),
    placeholderData: keepPreviousData,
  });

  const users = useMemo(() => data?.users ?? [], [data]);
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;
  const showPagination = totalPages > 1;

  const filtered = useMemo(() => {
    return users.filter((user) => {
      if (roleFilter !== "All" && user.role !== roleFilter) return false;
      return true;
    });
  }, [users, roleFilter]);

  const isFiltering = debouncedSearch !== "" || roleFilter !== "All";

  const goToPage = useCallback(
    (p: number) => setPage(Math.max(1, Math.min(p, totalPages))),
    [totalPages]
  );

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-fg">Users</h1>

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

      {isError ? (
        <div className="rounded-lg border border-status-cancelled-border bg-status-cancelled-bg p-8 text-center">
          <p className="text-sm text-danger">
            Failed to load users. The server may be unreachable.
          </p>
          <button
            onClick={() => refetch()}
            className="mt-3 inline-flex items-center rounded-md bg-status-cancelled-bg px-3 py-1.5 text-xs font-medium text-danger hover:bg-status-cancelled-border"
          >
            Try again
          </button>
        </div>
      ) : (
        <UsersTable
          users={filtered}
          isLoading={isLoading}
          emptyMessage={
            isFiltering ? "No users match your filters." : "No users found."
          }
        />
      )}

      {showPagination && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-fg-subtle">
            Showing {users.length > 0 ? (page - 1) * pageSize + 1 : 0}
            &ndash;{Math.min(page * pageSize, totalCount)} of {totalCount} users
          </p>
          <div className="flex items-center gap-1">
            <button
              onClick={() => goToPage(1)}
              disabled={page <= 1 || isPlaceholderData}
              className="rounded-md border border-border-strong px-2.5 py-1.5 text-xs font-medium text-fg-muted hover:bg-surface-2 disabled:opacity-40"
            >
              &laquo;
            </button>
            <button
              onClick={() => goToPage(page - 1)}
              disabled={page <= 1 || isPlaceholderData}
              className="rounded-md border border-border-strong px-2.5 py-1.5 text-xs font-medium text-fg-muted hover:bg-surface-2 disabled:opacity-40"
            >
              &lsaquo;
            </button>
            <span className="px-2 text-xs text-fg-subtle">
              {page} / {totalPages}
            </span>
            <button
              onClick={() => goToPage(page + 1)}
              disabled={page >= totalPages || isPlaceholderData}
              className="rounded-md border border-border-strong px-2.5 py-1.5 text-xs font-medium text-fg-muted hover:bg-surface-2 disabled:opacity-40"
            >
              &rsaquo;
            </button>
            <button
              onClick={() => goToPage(totalPages)}
              disabled={page >= totalPages || isPlaceholderData}
              className="rounded-md border border-border-strong px-2.5 py-1.5 text-xs font-medium text-fg-muted hover:bg-surface-2 disabled:opacity-40"
            >
              &raquo;
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
