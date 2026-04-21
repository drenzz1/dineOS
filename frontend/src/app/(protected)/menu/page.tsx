"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import {
  getMenuItems,
  getCategories,
  addCategory,
  deleteMenuItem,
} from "@/lib/api/menuApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { Button } from "@/components/ui/Button";
import MenuItemForm from "@/components/menu/MenuItemForm";
import CategoryTabs from "@/components/menu/CategoryTabs";
import type { MenuItem } from "@/types";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

const MenuItemTable = dynamic(
  () => import("@/components/menu/MenuItemTable"),
  { loading: () => <p>Loading table...</p> }
);


export default function MenuPage() {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [newCategory, setNewCategory] = useState("");
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<MenuItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<MenuItem | null>(null);

  const { data: categories = [] } = useQuery({
    queryKey: queryKeys.menuCategories.list(tenantId),
    queryFn: getCategories,
  });

  const { data: items = [] } = useQuery({
    queryKey: queryKeys.menu.list(tenantId),
    queryFn: getMenuItems,
  });

  const activeCategory = selectedCategory ?? categories[0] ?? null;
  const filteredItems = items.filter((i) => i.category === activeCategory);

  const { mutate: doAddCategory } = useMutation({
    mutationFn: addCategory,
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKeys.menuCategories.list(tenantId), updated);
      setNewCategory("");
    },
  });

  const { mutate: doDelete, isPending: isDeleting } = useMutation({
    mutationFn: deleteMenuItem,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menu.list(tenantId) });
      setDeleteTarget(null);
    },
  });

  function handleCategoryKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter" && newCategory.trim()) {
      doAddCategory(newCategory.trim());
    }
  }

  function handleFormClose() {
    setAddOpen(false);
    setEditTarget(null);
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Menu</h1>
        <Button onClick={() => setAddOpen(true)}>Add Item</Button>
      </div>

      {/* Category tabs */}
      <div className="space-y-3">
        <CategoryTabs
          categories={categories}
          selected={activeCategory ?? ""}
          onSelect={setSelectedCategory}
        />
        <input
          type="text"
          value={newCategory}
          onChange={(e) => setNewCategory(e.target.value)}
          onKeyDown={handleCategoryKeyDown}
          placeholder="New category — press Enter to add"
          aria-label="New category name"
          className="w-72 rounded-md border border-zinc-300 px-3 py-1.5 text-sm text-zinc-900 placeholder:text-zinc-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>

      {/* Items table */}
      {activeCategory !== null && (
        <MenuItemTable
          items={filteredItems}
          onEdit={(item) => setEditTarget(item)}
          onDelete={(item) => setDeleteTarget(item)}
        />
      )}

      {/* Add / Edit modal */}
      <Modal
        isOpen={addOpen || editTarget !== null}
        onClose={handleFormClose}
        title={editTarget ? "Edit Menu Item" : "Add Menu Item"}
      >
        <MenuItemForm
          onClose={handleFormClose}
          categories={categories}
          defaultValues={
            editTarget
              ? {
                  id: editTarget.id,
                  name: editTarget.name,
                  price: editTarget.price,
                  category: editTarget.category,
                  description: editTarget.description,
                }
              : undefined
          }
        />
      </Modal>

      {/* Delete confirmation modal */}
      <Modal
        isOpen={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        title="Delete item"
      >
        <div className="space-y-5">
          <p className="text-sm text-zinc-700">
            Are you sure you want to delete{" "}
            <span className="font-semibold">{deleteTarget?.name}</span>? This
            cannot be undone.
          </p>
          <div className="flex justify-end gap-3">
            <Button
              variant="secondary"
              onClick={() => setDeleteTarget(null)}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isDeleting}
              onClick={() =>
                deleteTarget && doDelete(deleteTarget.id)
              }
            >
              Delete
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
