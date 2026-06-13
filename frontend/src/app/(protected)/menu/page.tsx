"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import {
  getMenuItems,
  getCategories,
  addCategory,
  deleteMenuItem,
  saveMenuItem,
} from "@/lib/api/menuApi";
import { describeMenuItem } from "@/lib/api/aiApi";
import { handleApiError } from "@/lib/api/errorToast";
import { ApiError } from "@/lib/api/envelope";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { useAuthStore } from "@/stores/authStore";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Skeleton";
import MenuItemForm from "@/components/menu/MenuItemForm";
import CategoryTabs from "@/components/menu/CategoryTabs";
import MenuSemanticSearch from "@/components/menu/MenuSemanticSearch";
import type { MenuItem } from "@/types";

const Modal = dynamic(
  () => import("@/components/ui/Modal").then((m) => m.Modal),
  { ssr: false }
);

const MenuItemTable = dynamic(
  () => import("@/components/menu/MenuItemTable"),
  {
    loading: () => (
      <div className="rounded-md border border-border bg-surface shadow-sm">
        <div className="border-b border-border bg-surface-2 px-4 py-3">
          <Skeleton className="h-3 w-20" />
        </div>
        <div className="divide-y divide-border">
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="grid grid-cols-[1.4fr_0.6fr_2fr_0.8fr] items-center gap-4 px-4 py-3">
              <Skeleton className="h-3 w-32" />
              <Skeleton className="h-3 w-12" />
              <Skeleton className="h-3 w-48" />
              <Skeleton className="h-7 w-20 rounded-sm" />
            </div>
          ))}
        </div>
      </div>
    ),
  }
);

export default function MenuPage() {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const role = useAuthStore((s) => s.role);
  const isManager = role === "Manager" || role === "SuperAdmin";
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [newCategory, setNewCategory] = useState("");
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<MenuItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<MenuItem | null>(null);
  const [describeTarget, setDescribeTarget] = useState<MenuItem | null>(null);
  const [draftDescription, setDraftDescription] = useState<string | null>(null);
  const [describeErrorMsg, setDescribeErrorMsg] = useState<string | null>(null);

  const { data: categories = [], isLoading: isLoadingCategories } = useQuery({
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
    mutationFn: (name: string) => addCategory(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menuCategories.list(tenantId) });
      setNewCategory("");
    },
    onError: (error) => handleApiError(error),
  });

  const { mutate: doDelete, isPending: isDeleting } = useMutation({
    mutationFn: deleteMenuItem,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menu.list(tenantId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.menuItems.all });
      setDeleteTarget(null);
    },
    onError: (error) => handleApiError(error),
  });

  const { mutate: doDescribe, isPending: isDescribing } = useMutation({
    mutationFn: (item: MenuItem) => describeMenuItem(item.id),
    onSuccess: (suggestion) => {
      setDraftDescription(suggestion.suggestedDescription);
      setDescribeErrorMsg(null);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        if (error.status === 429) {
          setDescribeErrorMsg("Try again in a minute");
          return;
        }
        if (error.status === 422) {
          setDescribeErrorMsg("AI unavailable, fill in manually");
          return;
        }
      }
      setDescribeErrorMsg("Something went wrong, please try again");
    },
  });

  const { mutate: doSaveDescription, isPending: isSavingDescription } = useMutation({
    mutationFn: (item: MenuItem) =>
      saveMenuItem(
        { name: item.name, price: item.price, category: item.category, description: draftDescription ?? "" },
        item.id,
        item.imageUrl
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menu.list(tenantId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.menuItems.all });
      setDescribeTarget(null);
      setDraftDescription(null);
      setDescribeErrorMsg(null);
    },
    onError: (error) => handleApiError(error),
  });

  function openDescribeModal(item: MenuItem) {
    setDescribeTarget(item);
    setDraftDescription(null);
    setDescribeErrorMsg(null);
    doDescribe(item);
  }

  function closeDescribeModal() {
    setDescribeTarget(null);
    setDraftDescription(null);
    setDescribeErrorMsg(null);
  }

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
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
            Menu
          </h1>
          <p className="text-[13px] text-fg-muted mt-0.5">
            Organize dishes into categories and keep prices consistent across channels.
          </p>
        </div>
        <Button
          onClick={() => setAddOpen(true)}
          leading={
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 5v14M5 12h14" />
            </svg>
          }
        >
          Add Item
        </Button>
      </div>

      {/* Semantic search */}
      <MenuSemanticSearch onSelect={(item) => setEditTarget(item)} />

      {/* Category tabs + new category input */}
      <div className="space-y-3">
        <CategoryTabs
          categories={categories}
          selected={activeCategory ?? ""}
          onSelect={setSelectedCategory}
          isLoading={isLoadingCategories}
        />
        <input
          type="text"
          value={newCategory}
          onChange={(e) => setNewCategory(e.target.value)}
          onKeyDown={handleCategoryKeyDown}
          placeholder="New category — press Enter to add"
          aria-label="New category name"
          className="w-72 max-w-full h-[34px] rounded-sm border border-border-strong bg-surface px-3 text-[13px] text-fg placeholder:text-fg-subtle transition-[border-color,box-shadow] duration-150 focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-accent/25"
        />
      </div>

      {/* Items table */}
      {activeCategory !== null && (
        <MenuItemTable
          items={filteredItems}
          onEdit={(item) => setEditTarget(item)}
          onDelete={(item) => setDeleteTarget(item)}
          onDescribe={isManager ? openDescribeModal : undefined}
        />
      )}

      {/* Add / Edit modal */}
      <Modal
        isOpen={addOpen || editTarget !== null}
        onClose={handleFormClose}
        title={editTarget ? "Edit Menu Item" : "Add Menu Item"}
        width="xl"
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
                  imageUrl: editTarget.imageUrl,
                }
              : undefined
          }
        />
      </Modal>

      {/* AI Describe modal */}
      <Modal
        isOpen={describeTarget !== null}
        onClose={closeDescribeModal}
        title={`Describe with AI — ${describeTarget?.name ?? ""}`}
      >
        <div className="space-y-4">
          {isDescribing && (
            <div className="flex flex-col items-center gap-3 py-6">
              <svg
                className="h-5 w-5 animate-spin text-accent"
                viewBox="0 0 24 24"
                fill="none"
                aria-hidden="true"
              >
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
              </svg>
              <p className="text-[13px] text-fg-muted">Generating description…</p>
            </div>
          )}

          {!isDescribing && describeErrorMsg && (
            <div className="space-y-4">
              <p className="rounded-md border border-status-cancelled-border bg-status-cancelled-bg px-4 py-3 text-[13px] text-status-cancelled-fg">
                {describeErrorMsg}
              </p>
              <div className="flex justify-end">
                <Button variant="secondary" onClick={closeDescribeModal}>
                  Close
                </Button>
              </div>
            </div>
          )}

          {!isDescribing && draftDescription !== null && !describeErrorMsg && (
            <div className="space-y-3">
              <label htmlFor="ai-description" className="block text-[13px] font-medium text-fg">
                Suggested description
              </label>
              <textarea
                id="ai-description"
                rows={4}
                value={draftDescription}
                onChange={(e) => setDraftDescription(e.target.value)}
                className="block w-full resize-none rounded-sm border border-border-strong bg-surface px-3 py-2 text-[13px] text-fg placeholder:text-fg-subtle transition-[border-color,box-shadow] duration-150 focus:border-accent focus:outline-none focus:ring-[3px] focus:ring-accent/25"
              />
              <div className="flex justify-end gap-2">
                <Button variant="secondary" onClick={closeDescribeModal}>
                  Cancel
                </Button>
                <Button
                  isLoading={isSavingDescription}
                  onClick={() => describeTarget && doSaveDescription(describeTarget)}
                >
                  Save
                </Button>
              </div>
            </div>
          )}
        </div>
      </Modal>

      {/* Delete confirmation modal */}
      <Modal
        isOpen={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        title="Delete item"
      >
        <div className="space-y-5">
          <p className="text-[13px] text-fg-muted leading-relaxed">
            Are you sure you want to delete{" "}
            <span className="font-semibold text-fg">{deleteTarget?.name}</span>?
            This cannot be undone.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setDeleteTarget(null)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              isLoading={isDeleting}
              onClick={() => deleteTarget && doDelete(deleteTarget.id)}
            >
              Delete
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
