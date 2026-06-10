"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useAuthStore } from "@/stores/authStore";
import { loginSchema, type LoginFormValues } from "@/lib/validations/login";
import { ApiError } from "@/lib/api/envelope";
import { FirstLoginRequiredError } from "@/lib/auth/authApi";

export default function LoginPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const login = useAuthStore((state) => state.login);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: "", password: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    setFormError(null);
    const from = searchParams.get("from");
    try {
      // Business login establishes the account (owner) session; the operational
      // role is then chosen on the staff roster via PIN (#staff-pin-auth Phase
      // 3). SuperAdmins are bounced to /admin by middleware. `from` is carried
      // through so a deep link still resolves after staff selection.
      await login(values.username, values.password, from);
      // Use a full document request so Next middleware sees the freshly-written
      // auth cookies rather than the client router's pre-auth cached state.
      window.location.assign(from ? `/select-staff?from=${encodeURIComponent(from)}` : "/select-staff");
    } catch (err) {
      if (err instanceof FirstLoginRequiredError) {
        const params = new URLSearchParams({ email: err.email });
        if (from) params.set("from", from);
        router.push(`/first-login?${params.toString()}`);
        return;
      }
      if (err instanceof ApiError) {
        setFormError(err.error);
        return;
      }
      setFormError("Sign-in failed. Please try again.");
    }
  }

  return (
    <main id="main-content" className="flex min-h-screen items-center justify-center bg-zinc-50">
      <div className="w-full max-w-sm rounded-lg border border-zinc-200 bg-white p-8 shadow-sm space-y-6">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">Sign in to dineOS</h1>
          <p className="mt-1 text-sm text-zinc-500">Use your restaurant account.</p>
        </div>

        <form
          data-testid="login-form"
          noValidate
          onSubmit={handleSubmit(onSubmit)}
          className="space-y-4"
        >
          <Input
            id="login-username"
            label="Username"
            type="text"
            autoComplete="username"
            placeholder="manager@your-restaurant"
            error={errors.username?.message}
            {...register("username")}
          />
          <Input
            id="login-password"
            label="Password"
            type="password"
            autoComplete="current-password"
            placeholder="••••••••"
            error={errors.password?.message}
            {...register("password")}
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
            Sign in
          </Button>
        </form>

        <p className="text-center text-sm text-zinc-500">
          New to dineOS?{" "}
          <Link
            href="/signup"
            className="font-medium text-zinc-900 underline underline-offset-2"
          >
            Create an account — $50/mo
          </Link>
        </p>
      </div>
    </main>
  );
}
