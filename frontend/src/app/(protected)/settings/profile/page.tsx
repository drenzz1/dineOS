"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useTenant } from "@/hooks/useTenant";
import { useToast } from "@/hooks/useToast";
import { queryKeys } from "@/lib/api/queryKeys";
import {
  getRestaurantProfile,
  updateRestaurantProfile,
} from "@/lib/api/restaurantProfileApi";
import {
  restaurantProfileSchema,
  type RestaurantProfileFormValues,
} from "@/lib/validations/restaurantProfile";
import { ApiError } from "@/lib/api/envelope";

export default function RestaurantProfilePage() {
  const { tenantId } = useTenant();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: profile, isLoading } = useQuery({
    queryKey: queryKeys.restaurantProfile.current(tenantId),
    queryFn: getRestaurantProfile,
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<RestaurantProfileFormValues>({
    resolver: zodResolver(restaurantProfileSchema),
    defaultValues: { name: "", ownerName: "", phone: "", city: "" },
  });

  useEffect(() => {
    if (profile) {
      reset({
        name: profile.name,
        ownerName: profile.ownerName,
        phone: profile.phone,
        city: profile.city,
      });
    }
  }, [profile, reset]);

  const { mutate, isPending } = useMutation({
    mutationFn: updateRestaurantProfile,
    onSuccess: (updated) => {
      queryClient.setQueryData(
        queryKeys.restaurantProfile.current(tenantId),
        updated
      );
      reset({
        name: updated.name,
        ownerName: updated.ownerName,
        phone: updated.phone,
        city: updated.city,
      });
      toast({
        title: "Profile saved",
        description: "Restaurant profile updated successfully.",
        variant: "success",
        testId: "profile-toast-success",
      });
    },
    onError: (err) => {
      const message =
        err instanceof ApiError ? err.error : "Failed to save profile.";
      toast({
        title: "Save failed",
        description: message,
        variant: "error",
        testId: "profile-toast-error",
      });
    },
  });

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
          Restaurant profile
        </h1>
        <p className="mt-0.5 text-[13px] text-fg-muted">
          Update your restaurant&rsquo;s display name, owner, and contact details.
        </p>
      </div>

      {isLoading ? (
        <p className="text-[13px] text-fg-muted">Loading profile…</p>
      ) : (
        <form
          data-testid="profile-form"
          noValidate
          onSubmit={handleSubmit((values) => mutate(values))}
          className="space-y-4 bg-surface border border-border rounded-md p-5"
        >
          <Input
            id="profile-name"
            label="Restaurant name"
            type="text"
            error={errors.name?.message}
            {...register("name")}
          />
          <Input
            id="profile-owner"
            label="Owner name"
            type="text"
            error={errors.ownerName?.message}
            {...register("ownerName")}
          />
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Input
              id="profile-phone"
              label="Phone"
              type="tel"
              error={errors.phone?.message}
              {...register("phone")}
            />
            <Input
              id="profile-city"
              label="City"
              type="text"
              error={errors.city?.message}
              {...register("city")}
            />
          </div>

          {profile && (
            <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-[12px] text-fg-muted border-t border-border pt-4">
              <div>
                <dt className="text-fg-subtle">Slug</dt>
                <dd className="text-fg">{profile.slug}</dd>
              </div>
              <div>
                <dt className="text-fg-subtle">Owner email</dt>
                <dd className="text-fg">{profile.ownerEmail}</dd>
              </div>
              <div>
                <dt className="text-fg-subtle">Plan</dt>
                <dd className="text-fg">{profile.plan}</dd>
              </div>
              <div>
                <dt className="text-fg-subtle">Status</dt>
                <dd className="text-fg">{profile.status}</dd>
              </div>
            </dl>
          )}

          <div className="flex justify-end border-t border-border pt-4">
            <Button type="submit" isLoading={isPending} disabled={!isDirty}>
              Save changes
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
