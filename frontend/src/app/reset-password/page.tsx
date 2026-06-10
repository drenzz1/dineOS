"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import {
  resetPasswordSchema,
  type ResetPasswordFormValues,
} from "@/lib/validations/passwordReset";
import { ApiError } from "@/lib/api/envelope";
import { resetForgottenPassword } from "@/lib/auth/authApi";

function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);
  const [completed, setCompleted] = useState(false);

  const prefilledEmail = searchParams.get("email") ?? "";

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      email: prefilledEmail,
      code: "",
      newPassword: "",
      confirmPassword: "",
    },
  });

  async function onSubmit(values: ResetPasswordFormValues) {
    setFormError(null);
    try {
      await resetForgottenPassword(values.email, values.code, values.newPassword);
      setCompleted(true);
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.error);
        return;
      }
      setFormError("Password reset failed. Please try again.");
    }
  }

  if (completed) {
    return (
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6 text-center">
        <h1 className="text-xl font-semibold text-zinc-900">Password updated</h1>
        <p className="text-sm text-zinc-500">
          Your password has been reset. Sign in with your new password.
        </p>
        <Link
          href="/login"
          className="inline-block w-full rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white"
        >
          Go to sign in
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-zinc-900">Reset your password</h1>
        <p className="mt-1 text-sm text-zinc-500">
          Enter the 6-digit code we emailed you and choose a new password.
        </p>
      </div>

      <form
        data-testid="reset-password-form"
        noValidate
        onSubmit={handleSubmit(onSubmit)}
        className="space-y-4"
      >
        <Input
          id="reset-password-email"
          label="Email"
          type="email"
          autoComplete="username"
          error={errors.email?.message}
          {...register("email")}
        />
        <Input
          id="reset-password-code"
          label="Reset code"
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          placeholder="123456"
          error={errors.code?.message}
          {...register("code")}
        />
        <Input
          id="reset-password-new"
          label="New password"
          type="password"
          autoComplete="new-password"
          placeholder="At least 12 characters"
          error={errors.newPassword?.message}
          {...register("newPassword")}
        />
        <Input
          id="reset-password-confirm"
          label="Confirm new password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register("confirmPassword")}
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
          Reset password
        </Button>
      </form>

      <p className="text-center text-sm text-zinc-500">
        Didn&apos;t get a code?{" "}
        <Link
          href="/forgot-password"
          className="font-medium text-zinc-900 underline underline-offset-2"
        >
          Request a new one
        </Link>
      </p>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <main
      id="main-content"
      className="flex min-h-screen items-center justify-center bg-zinc-50"
    >
      <Suspense fallback={<div>Loading…</div>}>
        <ResetPasswordForm />
      </Suspense>
    </main>
  );
}
