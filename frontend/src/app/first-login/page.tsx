"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useAuthStore } from "@/stores/authStore";
import {
  firstLoginSchema,
  type FirstLoginFormValues,
} from "@/lib/validations/firstLogin";
import { ApiError } from "@/lib/api/envelope";
import { firstLoginPasswordChange } from "@/lib/auth/authApi";
import { getMe } from "@/lib/api/meApi";
import { getDestination, getPrimaryRole, persistAuthCookies, persistBusinessToken } from "@/lib/auth/keycloak";

function FirstLoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setAuth = useAuthStore((state) => state.setAuth);
  const [formError, setFormError] = useState<string | null>(null);

  const prefilledEmail = searchParams.get("email") ?? "";
  const from = searchParams.get("from");

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FirstLoginFormValues>({
    resolver: zodResolver(firstLoginSchema),
    defaultValues: {
      email: prefilledEmail,
      currentPassword: "",
      newPassword: "",
      confirmPassword: "",
    },
  });

  async function onSubmit(values: FirstLoginFormValues) {
    setFormError(null);
    try {
      const tokens = await firstLoginPasswordChange(
        values.email,
        values.currentPassword,
        values.newPassword
      );

      persistAuthCookies(
        tokens.accessToken,
        tokens.refreshToken,
        tokens.expiresIn,
        tokens.refreshExpiresIn,
        "Manager",
        null
      );

      const me = await getMe();
      const role = getPrimaryRole(me.roles);

      persistAuthCookies(
        tokens.accessToken,
        tokens.refreshToken,
        tokens.expiresIn,
        tokens.refreshExpiresIn,
        role,
        me.tenantId
      );

      persistBusinessToken(tokens.accessToken, tokens.refreshExpiresIn ?? tokens.expiresIn);
      setAuth(me.id, role, me.tenantId, null, tokens.accessToken);
      window.location.assign(getDestination(role, from));
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.error);
        return;
      }
      setFormError("Password change failed. Please try again.");
    }
  }

  return (
    <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-zinc-900">Set your password</h1>
        <p className="mt-1 text-sm text-zinc-500">
          The password we emailed you is temporary. Choose a permanent one to
          finish setting up your account.
        </p>
      </div>

      <form
        data-testid="first-login-form"
        noValidate
        onSubmit={handleSubmit(onSubmit)}
        className="space-y-4"
      >
        <Input
          id="first-login-email"
          label="Email"
          type="email"
          autoComplete="username"
          error={errors.email?.message}
          {...register("email")}
        />
        <Input
          id="first-login-current-password"
          label="Temporary password"
          type="password"
          autoComplete="current-password"
          error={errors.currentPassword?.message}
          {...register("currentPassword")}
        />
        <Input
          id="first-login-new-password"
          label="New password"
          type="password"
          autoComplete="new-password"
          placeholder="At least 12 characters"
          error={errors.newPassword?.message}
          {...register("newPassword")}
        />
        <Input
          id="first-login-confirm-password"
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
          Update password &amp; sign in
        </Button>
      </form>

      <p className="text-center text-sm text-zinc-500">
        Already updated your password?{" "}
        <Link
          href="/login"
          className="font-medium text-zinc-900 underline underline-offset-2"
        >
          Sign in
        </Link>
      </p>
    </div>
  );
}

export default function FirstLoginPage() {
  return (
    <main
      id="main-content"
      className="flex min-h-screen items-center justify-center bg-zinc-50"
    >
      <Suspense fallback={<div>Loading…</div>}>
        <FirstLoginForm />
      </Suspense>
    </main>
  );
}
