"use client";

import { useState } from "react";
import { useSemanticMenuSearch } from "@/hooks/useSemanticMenuSearch";
import { ApiError } from "@/lib/api/envelope";
import type { MenuItem } from "@/types";

interface MenuSemanticSearchProps {
  onSelect?: (item: MenuItem) => void;
}

export default function MenuSemanticSearch({ onSelect }: MenuSemanticSearchProps) {
  const [query, setQuery] = useState("");
  const { mutate: search, data: results, isPending, error, reset } = useSemanticMenuSearch();

  const apiError = error instanceof ApiError ? error : null;
  const isUnconfigured = apiError?.status === 422;

  function handleSearch() {
    const q = query.trim();
    if (!q) return;
    reset();
    search(q);
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") handleSearch();
  }

  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        <div className="relative flex-1">
          <input
            type="search"
            value={query}
            onChange={(e) => { setQuery(e.target.value); reset(); }}
            onKeyDown={handleKeyDown}
            placeholder="Search by meaning — e.g. &ldquo;spicy grilled chicken&rdquo;"
            aria-label="Semantic menu search"
            className="w-full h-[34px] rounded-sm border border-border bg-surface pl-8 pr-3 text-[13px] text-fg placeholder:text-fg-subtle transition-[border-color,box-shadow] duration-150 focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-accent/25"
          />
          <svg
            className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-fg-subtle"
            viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
            strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.35-4.35" />
          </svg>
        </div>
        <button
          type="button"
          onClick={handleSearch}
          disabled={!query.trim() || isPending}
          className="h-[34px] rounded-sm border border-border bg-surface px-3 text-[13px] font-medium text-fg transition-colors hover:border-border-strong hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isPending ? "Searching…" : "Search"}
        </button>
      </div>

      {isUnconfigured && (
        <p className="text-[12px] text-fg-muted">
          Semantic search is not configured. Visit Admin → Settings to choose an embeddings provider.
        </p>
      )}

      {!isUnconfigured && apiError && (
        <p className="text-[12px] text-status-cancelled-fg">Search failed. Please try again.</p>
      )}

      {results && results.length === 0 && (
        <p className="text-[12px] text-fg-muted">No matching items found.</p>
      )}

      {results && results.length > 0 && (
        <ul className="divide-y divide-border rounded-sm border border-border bg-surface">
          {results.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => onSelect?.(item)}
                className="flex w-full items-center justify-between gap-4 px-4 py-2.5 text-left transition-colors hover:bg-surface-2"
              >
                <div className="min-w-0">
                  <p className="truncate text-[13px] font-medium text-fg">{item.name}</p>
                  {item.description && (
                    <p className="mt-0.5 truncate text-[12px] text-fg-muted">{item.description}</p>
                  )}
                </div>
                <span className="shrink-0 text-[13px] font-medium text-fg">
                  ${item.price.toFixed(2)}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
