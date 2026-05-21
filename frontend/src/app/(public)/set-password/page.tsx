"use client";

import { Suspense, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/hooks/useToast";
import {
  setPasswordSchema,
  type SetPasswordFormValues,
} from "@/lib/validations/setPassword";
import { setPassword, type SetPasswordPayload } from "@/lib/api/signupApi";
import { ApiError } from "@/lib/api/envelope";

type PanelTone = "success" | "info" | "warning" | "error";

export default function SetPasswordPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-md items-center justify-center px-6 py-12">
      <Suspense
        fallback={
          <Panel title="Loading…" tone="info">
            <p className="text-sm text-fg-muted">Preparing your account…</p>
          </Panel>
        }
      >
        <SetPasswordInner />
      </Suspense>
    </main>
  );
}

function SetPasswordInner() {
  const search = useSearchParams();
  const router = useRouter();
  const { toast } = useToast();
  const token = search.get("token") ?? "";

  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SetPasswordFormValues>({
    resolver: zodResolver(setPasswordSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  const mutation = useMutation<string, unknown, SetPasswordPayload>({
    mutationFn: setPassword,
    onSuccess: () => {
      setDone(true);
      toast({
        title: "Password set",
        description: "You can sign in with your new password.",
        variant: "success",
      });
      router.push("/login?passwordSet=1");
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 404) {
        toast({
          title: "Link expired",
          description:
            "This setup link is no longer valid. Please contact support to resend.",
          variant: "error",
        });
        return;
      }
      // 400 / 422 / 429 / 500 are surfaced by the global MutationCache → handleApiError bridge.
    },
  });

  function onSubmit(values: SetPasswordFormValues) {
    mutation.mutate({ token, newPassword: values.newPassword });
  }

  if (!token) {
    return (
      <Panel title="Missing setup link" tone="warning">
        <p className="text-sm text-fg-muted">
          We couldn&apos;t find your setup token. Open the link from the
          welcome email we sent you, or{" "}
          <Link href="/signup" className="underline underline-offset-2">
            start over
          </Link>
          .
        </p>
      </Panel>
    );
  }

  if (done) {
    return (
      <Panel title="Password set" tone="success">
        <p className="text-sm text-fg-muted">
          Redirecting you to sign in…
        </p>
      </Panel>
    );
  }

  return (
    <section className="w-full rounded-2xl bg-surface p-8 ring-1 ring-border">
      <h1 className="text-2xl font-semibold tracking-tight text-fg">
        Set your password
      </h1>
      <p className="mt-2 text-sm text-fg-muted">
        Choose a password to finish setting up your dineOS account. Minimum 12
        characters, with an uppercase letter, a lowercase letter, and a digit.
      </p>

      <form
        noValidate
        onSubmit={handleSubmit(onSubmit)}
        className="mt-6 grid gap-5"
      >
        <Input
          id="newPassword"
          label="New password"
          type="password"
          autoComplete="new-password"
          error={errors.newPassword?.message}
          {...register("newPassword")}
        />
        <Input
          id="confirmPassword"
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register("confirmPassword")}
        />

        <Button
          type="submit"
          block
          isLoading={mutation.isPending || isSubmitting}
        >
          Set password
        </Button>

        <p className="text-xs text-fg-subtle">
          By continuing you agree to dineOS&apos; terms. Already have an
          account?{" "}
          <Link
            href="/login"
            className="underline underline-offset-2 text-fg-muted"
          >
            Sign in
          </Link>
          .
        </p>
      </form>
    </section>
  );
}

interface PanelProps {
  title: string;
  tone: PanelTone;
  children: ReactNode;
}

function Panel({ title, tone, children }: PanelProps) {
  const ringByTone: Record<PanelTone, string> = {
    success: "ring-status-ready-solid/30 bg-surface",
    info: "ring-border bg-surface",
    warning: "ring-status-stalled-amber-solid/30 bg-surface",
    error: "ring-status-cancelled-solid/30 bg-surface",
  };
  return (
    <section className={`w-full rounded-2xl p-8 ring-1 ${ringByTone[tone]}`}>
      <h1 className="text-xl font-semibold text-fg">{title}</h1>
      <div className="mt-3 text-sm text-fg-muted">{children}</div>
    </section>
  );
}
