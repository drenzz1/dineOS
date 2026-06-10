"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import {
  forgotPasswordSchema,
  type ForgotPasswordFormValues,
} from "@/lib/validations/passwordReset";
import { ApiError } from "@/lib/api/envelope";
import { requestPasswordReset } from "@/lib/auth/authApi";

export default function ForgotPasswordPage() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  });

  async function onSubmit(values: ForgotPasswordFormValues) {
    setFormError(null);
    try {
      await requestPasswordReset(values.email);
      // No cookies are written here, so client-side navigation is fine.
      router.push(`/reset-password?email=${encodeURIComponent(values.email)}`);
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.error);
        return;
      }
      setFormError("Could not send the reset code. Please try again.");
    }
  }

  return (
    <main
      id="main-content"
      className="flex min-h-screen items-center justify-center bg-zinc-50"
    >
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">
            Forgot your password?
          </h1>
          <p className="mt-1 text-sm text-zinc-500">
            Enter your account email. If an account exists, we&apos;ll email
            you a 6-digit code to reset your password.
          </p>
        </div>

        <form
          data-testid="forgot-password-form"
          noValidate
          onSubmit={handleSubmit(onSubmit)}
          className="space-y-4"
        >
          <Input
            id="forgot-password-email"
            label="Email"
            type="email"
            autoComplete="username"
            placeholder="manager@your-restaurant"
            error={errors.email?.message}
            {...register("email")}
          />

          {formError && (
            <p
              role="alert"
              className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-[12px] text-red-700"
            >
              {formError}
            </p>
          )}

          <Button type="submit" block isLoading={isSubmitting}>
            Send reset code
          </Button>
        </form>

        <p className="text-center text-sm text-zinc-500">
          Remembered it?{" "}
          <Link
            href="/login"
            className="font-medium text-zinc-900 underline underline-offset-2"
          >
            Back to sign in
          </Link>
        </p>
      </div>
    </main>
  );
}
