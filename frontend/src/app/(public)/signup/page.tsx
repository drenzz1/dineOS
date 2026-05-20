"use client";

import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/hooks/useToast";
import { signupSchema, type SignupFormValues } from "@/lib/validations/signup";
import { startSignup, type SignupResult } from "@/lib/api/signupApi";
import { ApiError } from "@/lib/api/envelope";

const SIGNUP_SESSION_KEY = "dineos.signup.lastSessionId";

export default function SignupPage() {
  const { toast } = useToast();

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

  const mutation = useMutation<SignupResult, unknown, SignupFormValues>({
    mutationFn: startSignup,
    onSuccess: (result) => {
      try {
        sessionStorage.setItem(SIGNUP_SESSION_KEY, result.sessionId);
      } catch {
        // sessionStorage may be unavailable in some browsing modes; safe to ignore.
      }
      // Hard navigation: Stripe Checkout is cross-origin, router.push won't work.
      window.location.assign(result.checkoutUrl);
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 503) {
        toast({
          title: "Billing temporarily unavailable",
          description:
            "Our payment provider is down. Please try again in a few minutes.",
          variant: "error",
        });
        return;
      }
      // 400 / 422 / 429 / 500 are surfaced by the global MutationCache → handleApiError bridge.
    },
  });

  function onSubmit(values: SignupFormValues): void {
    mutation.mutate(values);
  }

  return (
    <main
      id="main-content"
      className="mx-auto grid min-h-screen max-w-5xl gap-10 px-6 py-12 md:grid-cols-[1fr_22rem]"
    >
      <section>
        <h1 className="text-3xl font-semibold tracking-tight text-fg">
          Start your restaurant on dineOS
        </h1>
        <p className="mt-2 text-sm text-fg-muted">
          No trial — full access from day one for $50/month.
        </p>

        <form
          noValidate
          onSubmit={handleSubmit(onSubmit)}
          className="mt-8 grid gap-5"
        >
          <Input
            id="restaurantName"
            label="Restaurant name"
            autoComplete="organization"
            error={errors.restaurantName?.message}
            {...register("restaurantName")}
          />
          <Input
            id="ownerName"
            label="Owner name"
            autoComplete="name"
            error={errors.ownerName?.message}
            {...register("ownerName")}
          />
          <Input
            id="ownerEmail"
            label="Owner email"
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

          <Button
            type="submit"
            block
            isLoading={mutation.isPending || isSubmitting}
          >
            Continue to payment
          </Button>

          <p className="text-xs text-fg-subtle">
            You&apos;ll be redirected to Stripe to complete payment. After
            payment, we&apos;ll email you a temporary password to sign in.
          </p>
        </form>
      </section>

      <aside className="h-fit rounded-2xl border border-border bg-surface-2 p-6">
        <h2 className="text-lg font-semibold text-fg">dineOS Pro</h2>
        <p className="mt-1 text-3xl font-bold text-fg">
          $50
          <span className="text-base font-medium text-fg-muted">/month</span>
        </p>
        <ul className="mt-4 space-y-2 text-sm text-fg-muted">
          <li>• Unlimited orders &amp; tables</li>
          <li>• Realtime kitchen board</li>
          <li>• Staff &amp; shift management</li>
          <li>• Stripe-powered payments</li>
          <li>• Email + chat support</li>
        </ul>
        <p className="mt-6 text-xs text-fg-subtle">
          Cancel any time. Billed monthly.
        </p>
        <p className="mt-4 text-sm text-fg-muted">
          Already have an account?{" "}
          <Link
            href="/login"
            className="font-medium text-fg underline underline-offset-2"
          >
            Sign in
          </Link>
        </p>
      </aside>
    </main>
  );
}
