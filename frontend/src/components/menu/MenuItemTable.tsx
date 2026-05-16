"use client";

import type { MenuItem } from "@/types";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";

interface MenuItemTableProps {
  items: MenuItem[];
  onEdit: (item: MenuItem) => void;
  onDelete: (item: MenuItem) => void;
  onDescribe?: (item: MenuItem) => void;
}

export default function MenuItemTable({
  items,
  onEdit,
  onDelete,
  onDescribe,
}: MenuItemTableProps) {
  if (items.length === 0) {
    return (
      <div className="rounded-md border border-dashed border-border-strong bg-surface">
        <EmptyState
          illustration={<Illo.Menu />}
          title="No items in this category"
          description="Add your first item to make it available to the floor and kitchen."
        />
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border border-border bg-surface shadow-sm">
      <table className="w-full text-[13px]">
        <thead className="border-b border-border bg-surface-2 text-left">
          <tr>
            <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
              Name
            </th>
            <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
              Price
            </th>
            <th className="hidden px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle md:table-cell">
              Description
            </th>
            <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-fg-subtle">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {items.map((item) => (
            <tr
              key={item.id}
              className="transition-colors duration-150 hover:bg-surface-2"
            >
              <td className="px-4 py-3 font-medium text-fg">{item.name}</td>
              <td className="px-4 py-3 text-fg-muted dos-num">
                ${item.price.toFixed(2)}
              </td>
              <td className="hidden px-4 py-3 text-fg-muted md:table-cell">
                {item.description ?? <span className="text-fg-subtle">—</span>}
              </td>
              <td className="px-4 py-3 text-right">
                <div className="flex items-center justify-end gap-2">
                  {onDescribe && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => onDescribe(item)}
                    >
                      ✨ Describe
                    </Button>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onEdit(item)}
                  >
                    Edit
                  </Button>
                  <Button
                    variant="danger"
                    size="sm"
                    onClick={() => onDelete(item)}
                  >
                    Delete
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
