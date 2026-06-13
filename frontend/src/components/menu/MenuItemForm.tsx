"use client";

import { useState } from "react";
import Image from "next/image";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { menuItemSchema } from "@/lib/validations/menuItem";
import type { MenuItemFormValues } from "@/lib/validations/menuItem";
import { saveMenuItem, uploadMenuItemImage } from "@/lib/api/menuApi";
import { handleApiError } from "@/lib/api/errorToast";
import { queryKeys } from "@/lib/api/queryKeys";
import { useTenant } from "@/hooks/useTenant";
import { MenuCategory } from "@/types";
import { Button } from "@/components/ui/Button";

const FIELD_CLASS =
  "block h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-fg outline-none transition placeholder:text-fg-subtle focus:border-accent";
const LABEL_CLASS = "block text-xs font-semibold text-fg-muted";

interface MenuItemFormProps {
  onClose: () => void;
  categories?: string[];
  defaultValues?: Partial<MenuItemFormValues> & { id?: string; imageUrl?: string };
}

export default function MenuItemForm({ onClose, categories, defaultValues }: MenuItemFormProps) {
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [dragOver, setDragOver] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    control,
    setValue,
    formState: { errors },
  } = useForm<MenuItemFormValues>({
    resolver: zodResolver(menuItemSchema),
    defaultValues: {
      name: "",
      description: "",
      ...defaultValues,
    },
  });

  const { mutate, isPending } = useMutation({
    mutationFn: async (data: MenuItemFormValues) => {
      const item = await saveMenuItem(data, defaultValues?.id, defaultValues?.imageUrl);
      if (data.imageFile instanceof File) {
        await uploadMenuItemImage(item.id, data.imageFile);
      }
      return item;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menu.list(tenantId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.menuItems.all });
      onClose();
    },
    onError: (error) => {
      handleApiError(error);
    },
  });

  function handleFile(file: File | undefined) {
    if (!file) return;
    setValue("imageFile", file, { shouldValidate: true });
    if (preview) URL.revokeObjectURL(preview);
    setPreview(URL.createObjectURL(file));
  }

  // Not memoized: a useCallback with an empty dep list captured the
  // first-render handleFile (and its preview), so later drops never revoked
  // the previous object URL.
  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setDragOver(false);
    handleFile(e.dataTransfer.files[0]);
  }

  function clearFile() {
    setValue("imageFile", undefined, { shouldValidate: false });
    if (preview) URL.revokeObjectURL(preview);
    setPreview(null);
  }

  const imageFile = useWatch({ control, name: "imageFile" });

  const existingImageUrl = defaultValues?.imageUrl;
  const showExistingImage = existingImageUrl && !imageFile && !preview;

  return (
    <form onSubmit={handleSubmit((d) => mutate(d))} noValidate className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_160px]">
        <div className="space-y-1">
          <label htmlFor="mi-name" className={LABEL_CLASS}>
            Item name
          </label>
          <input
            id="mi-name"
            type="text"
            {...register("name")}
            placeholder="e.g. Margherita Pizza"
            className={FIELD_CLASS}
          />
          {errors.name && (
            <p className="text-xs text-danger">{errors.name.message}</p>
          )}
        </div>

        <div className="space-y-1">
          <label htmlFor="mi-price" className={LABEL_CLASS}>
            Price
          </label>
          <div className="relative">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-fg-subtle">
              $
            </span>
            <input
              id="mi-price"
              type="number"
              min={0}
              step={0.01}
              {...register("price", {
                setValueAs: (v: string) =>
                  v === "" ? undefined : parseFloat(v),
              })}
              placeholder="0.00"
              className={`${FIELD_CLASS} pl-7`}
            />
          </div>
          {errors.price && (
            <p className="text-xs text-danger">{errors.price.message}</p>
          )}
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-1">
          <label htmlFor="mi-category" className={LABEL_CLASS}>
            Category
          </label>
          <select
            id="mi-category"
            {...register("category")}
            className={FIELD_CLASS}
          >
            <option value="">Select a category</option>
            {(categories ?? Object.values(MenuCategory)).map((cat) => (
              <option key={cat} value={cat}>
                {cat}
              </option>
            ))}
          </select>
          {errors.category && (
            <p className="text-xs text-danger">{errors.category.message}</p>
          )}
        </div>

        <div className="space-y-1">
          <label htmlFor="mi-desc" className={LABEL_CLASS}>
            Description{" "}
            <span className="font-normal text-fg-subtle">(optional)</span>
          </label>
          <textarea
            id="mi-desc"
            rows={3}
            {...register("description")}
            placeholder="Brief description..."
            className="block min-h-20 w-full resize-none rounded-md border border-border bg-surface px-3 py-2 text-sm text-fg outline-none transition placeholder:text-fg-subtle focus:border-accent"
          />
          {errors.description && (
            <p className="text-xs text-danger">{errors.description.message}</p>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <p className={LABEL_CLASS}>
          Image{" "}
          <span className="font-normal text-fg-subtle">
            (optional, max 2 MB · JPEG / PNG / WebP)
          </span>
        </p>

        {showExistingImage && (
          <div className="flex items-center gap-3 rounded-md border border-border bg-surface-2 p-3">
            <Image
              src={existingImageUrl}
              alt="Current image"
              width={48}
              height={48}
              unoptimized
              className="h-12 w-12 rounded object-cover"
            />
            <p className="min-w-0 flex-1 truncate text-sm text-fg-muted">Current image</p>
          </div>
        )}

        {imageFile ? (
          <div className="flex items-center gap-3 rounded-md border border-border bg-surface-2 p-3">
            {preview && (
              <Image
                src={preview}
                alt="Preview"
                width={48}
                height={48}
                unoptimized
                className="h-12 w-12 rounded object-cover"
              />
            )}
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-fg">
                {imageFile.name}
              </p>
              <p className="text-xs text-fg-muted">
                {(imageFile.size / 1024).toFixed(1)} KB
              </p>
            </div>
            <button
              type="button"
              onClick={clearFile}
              className="text-xs font-semibold text-danger"
            >
              Remove
            </button>
          </div>
        ) : (
          <div
            onDragOver={(e) => {
              e.preventDefault();
              setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleDrop}
            className={`flex min-h-24 flex-col items-center justify-center rounded-lg border border-dashed px-4 py-4 text-center transition-colors ${
              dragOver
                ? "border-accent bg-accent-soft"
                : "border-border-strong bg-surface-2"
            }`}
          >
            <p className="text-sm text-fg-muted">
              Drag &amp; drop an image, or{" "}
              <label className="cursor-pointer font-semibold text-accent underline">
                browse
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="sr-only"
                  onChange={(e) => handleFile(e.target.files?.[0])}
                />
              </label>
            </p>
            <p className="mt-1 text-xs text-fg-subtle">PNG, JPG, WebP up to 2 MB</p>
          </div>
        )}
        {errors.imageFile?.message && (
          <p className="text-xs text-danger">{String(errors.imageFile.message)}</p>
        )}
      </div>

      <div className="flex justify-end gap-2 border-t border-border pt-4">
        <Button type="button" variant="secondary" onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit" isLoading={isPending}>
          Save
        </Button>
      </div>
    </form>
  );
}
