"use client";

import { Skeleton } from "@/components/ui/Skeleton";

interface CategoryTabsProps {
  categories: string[];
  selected: string;
  onSelect: (category: string) => void;
  isLoading?: boolean;
}

export function CategoryTabsSkeleton() {
  return (
    <div className="flex flex-wrap gap-2">
      {[0, 1, 2, 3].map((i) => (
        <Skeleton key={i} className="h-7 w-24 rounded-full" />
      ))}
    </div>
  );
}

export default function CategoryTabs({
  categories,
  selected,
  onSelect,
  isLoading = false,
}: CategoryTabsProps) {
  if (isLoading) return <CategoryTabsSkeleton />;
  if (categories.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-1.5">
      {categories.map((cat) => {
        const isActive = selected === cat;
        return (
          <button
            key={cat}
            type="button"
            onClick={() => onSelect(cat)}
            aria-pressed={isActive}
            className={`rounded-full border px-3 h-7 text-[12px] font-semibold transition-colors duration-150 ${
              isActive
                ? "bg-accent text-accent-fg border-accent"
                : "bg-surface text-fg-muted border-border hover:bg-surface-2 hover:text-fg hover:border-border-strong"
            }`}
          >
            {cat}
          </button>
        );
      })}
    </div>
  );
}
