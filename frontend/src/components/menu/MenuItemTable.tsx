"use client";

import type { MenuItem } from "@/types";
import { Button } from "@/components/ui/Button";

interface MenuItemTableProps {
  items: MenuItem[];
  onEdit: (item: MenuItem) => void;
  onDelete: (item: MenuItem) => void;
}

export default function MenuItemTable({
  items,
  onEdit,
  onDelete,
}: MenuItemTableProps) {
  if (items.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-zinc-300 p-12 text-center">
        <p className="text-sm text-zinc-500">No items in this category yet.</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-lg border border-zinc-200">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-zinc-200 bg-zinc-50 text-left">
            <th className="px-4 py-3 font-medium text-zinc-500">Name</th>
            <th className="px-4 py-3 font-medium text-zinc-500">Price</th>
            <th className="hidden px-4 py-3 font-medium text-zinc-500 md:table-cell">
              Description
            </th>
            <th className="px-4 py-3 text-right font-medium text-zinc-500">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-200">
          {items.map((item) => (
            <tr key={item.id} className="bg-white hover:bg-zinc-50">
              <td className="px-4 py-3 font-medium text-zinc-900">
                {item.name}
              </td>
              <td className="px-4 py-3 text-zinc-600">
                ${item.price.toFixed(2)}
              </td>
              <td className="hidden px-4 py-3 text-zinc-500 md:table-cell">
                {item.description ?? (
                  <span className="text-zinc-300">—</span>
                )}
              </td>
              <td className="px-4 py-3 text-right">
                <div className="flex items-center justify-end gap-2">
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
