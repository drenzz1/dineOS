"use client";

import { useCallback, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { menuItemSchema } from "@/lib/validations/menuItem";
import type { MenuItemFormValues } from "@/lib/validations/menuItem";
import { saveMenuItem } from "@/lib/api/menuApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { MenuCategory } from "@/types";
import { Button } from "@/components/ui/Button";

interface MenuItemFormProps {
  onClose: () => void;
  defaultValues?: Partial<MenuItemFormValues> & { id?: string };
}

export default function MenuItemForm({ onClose, defaultValues }: MenuItemFormProps) {
  const queryClient = useQueryClient();
  const [dragOver, setDragOver] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
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
    mutationFn: (data: MenuItemFormValues) =>
      saveMenuItem(data, defaultValues?.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.menu.list() });
      onClose();
    },
  });

  function handleFile(file: File | undefined) {
    if (!file) return;
    setValue("imageFile", file, { shouldValidate: true });
    if (preview) URL.revokeObjectURL(preview);
    setPreview(URL.createObjectURL(file));
  }

  const handleDrop = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragOver(false);
    handleFile(e.dataTransfer.files[0]);
  }, []);

  function clearFile() {
    setValue("imageFile", undefined, { shouldValidate: false });
    if (preview) URL.revokeObjectURL(preview);
    setPreview(null);
  }

  const imageFile = watch("imageFile");

  return (
    <form onSubmit={handleSubmit((d) => mutate(d))} noValidate className="space-y-4">
      {/* Name */}
      <div className="space-y-1">
        <label htmlFor="mi-name" className="block text-sm font-medium text-zinc-700">
          Item name
        </label>
        <input
          id="mi-name"
          type="text"
          {...register("name")}
          placeholder="e.g. Margherita Pizza"
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {errors.name && (
          <p className="text-sm text-red-600">{errors.name.message}</p>
        )}
      </div>

      {/* Price */}
      <div className="space-y-1">
        <label htmlFor="mi-price" className="block text-sm font-medium text-zinc-700">
          Price
        </label>
        <div className="relative">
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-zinc-400">
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
            className="block w-full rounded-md border border-zinc-300 py-2 pl-7 pr-3 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>
        {errors.price && (
          <p className="text-sm text-red-600">{errors.price.message}</p>
        )}
      </div>

      {/* Category */}
      <div className="space-y-1">
        <label htmlFor="mi-category" className="block text-sm font-medium text-zinc-700">
          Category
        </label>
        <select
          id="mi-category"
          {...register("category")}
          className="block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        >
          <option value="">Select a category</option>
          {Object.values(MenuCategory).map((cat) => (
            <option key={cat} value={cat}>
              {cat}
            </option>
          ))}
        </select>
        {errors.category && (
          <p className="text-sm text-red-600">{errors.category.message}</p>
        )}
      </div>

      {/* Description */}
      <div className="space-y-1">
        <label htmlFor="mi-desc" className="block text-sm font-medium text-zinc-700">
          Description{" "}
          <span className="font-normal text-zinc-400">(optional)</span>
        </label>
        <textarea
          id="mi-desc"
          rows={2}
          {...register("description")}
          placeholder="Brief description..."
          className="block w-full resize-none rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        {errors.description && (
          <p className="text-sm text-red-600">{errors.description.message}</p>
        )}
      </div>

      {/* Image upload */}
      <div className="space-y-2">
        <p className="text-sm font-medium text-zinc-700">
          Image{" "}
          <span className="font-normal text-zinc-400">(optional, max 2 MB)</span>
        </p>

        {imageFile ? (
          <div className="flex items-center gap-3 rounded-md border border-zinc-200 bg-zinc-50 p-3">
            {preview && (
              <img
                src={preview}
                alt="Preview"
                className="h-12 w-12 rounded object-cover"
              />
            )}
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-zinc-900">
                {imageFile.name}
              </p>
              <p className="text-xs text-zinc-500">
                {(imageFile.size / 1024).toFixed(1)} KB
              </p>
            </div>
            <button
              type="button"
              onClick={clearFile}
              className="text-xs text-red-500 hover:text-red-700"
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
            className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed px-4 py-6 text-center transition-colors ${
              dragOver
                ? "border-blue-400 bg-blue-50"
                : "border-zinc-300 bg-zinc-50"
            }`}
          >
            <p className="text-sm text-zinc-500">
              Drag &amp; drop an image, or{" "}
              <label className="cursor-pointer text-blue-600 underline hover:text-blue-700">
                browse
                <input
                  type="file"
                  accept="image/*"
                  className="sr-only"
                  onChange={(e) => handleFile(e.target.files?.[0])}
                />
              </label>
            </p>
            <p className="mt-1 text-xs text-zinc-400">PNG, JPG, GIF up to 2 MB</p>
          </div>
        )}
        {errors.imageFile?.message && (
          <p className="text-sm text-red-600">{String(errors.imageFile.message)}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 border-t border-zinc-200 pt-4">
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
