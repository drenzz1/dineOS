"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { signupSchema, type SignupFormValues } from "@/lib/validations/signup";
import { startSignup } from "@/lib/api/signupApi";
import { ApiError } from "@/lib/api/envelope";

export default function SignupPage() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignupFormValues>({
    resolver: zodResolver(signupSchema),
    defaultValues: {
      restaurantName: "",
      ownerName: "",
      ownerEmail: "",
      phone: "",
      city: "",
    },
  });

  async function onSubmit(values: SignupFormValues) {
    setFormError(null);
    try {
      const { sessionId } = await startSignup(values);
      router.push(`/signup/success?sessionId=${encodeURIComponent(sessionId)}`);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 503) {
          setFormError("Our billing system is temporarily unavailable. Please try again in a few minutes.");
        } else {
          setFormError(err.error);
        }
        return;
      }
      setFormError("Something went wrong. Please try again.");
    }
  }

  return (
    <main id="main-content" className="flex min-h-screen items-center justify-center bg-zinc-50">
      <div className="w-full max-w-md rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">Start your free trial</h1>
          <p className="mt-1 text-sm text-zinc-500">
            Set up your restaurant on dineOS. You&apos;ll complete payment on the next step.
          </p>
        </div>

        <form noValidate onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <Input
            id="restaurantName"
            label="Restaurant name"
            autoComplete="organization"
            error={errors.restaurantName?.message}
            {...register("restaurantName")}
          />
          <Input
            id="ownerName"
            label="Your name"
            autoComplete="name"
            error={errors.ownerName?.message}
            {...register("ownerName")}
          />
          <Input
            id="ownerEmail"
            label="Email address"
            type="email"
            autoComplete="email"
            error={errors.ownerEmail?.message}
            {...register("ownerEmail")}
          />
          <Input
            id="phone"
            label="Phone"
            type="tel"
            autoComplete="tel"
            error={errors.phone?.message}
            {...register("phone")}
          />
          <Input
            id="city"
            label="City"
            autoComplete="address-level2"
            error={errors.city?.message}
            {...register("city")}
          />

          {formError && (
            <p role="alert" className="text-sm text-red-600">
              {formError}
            </p>
          )}

          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? "Please wait…" : "Continue to payment"}
          </Button>
        </form>

        <p className="text-center text-sm text-zinc-500">
          Already have an account?{" "}
          <a href="/login" className="text-zinc-900 underline underline-offset-2">
            Sign in
          </a>
        </p>
      </div>
    </main>
  );
}
