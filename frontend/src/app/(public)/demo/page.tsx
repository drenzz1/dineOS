"use client";

import { useState } from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/hooks/useToast";
import {
  demoRequestSchema,
  type DemoRequestFormValues,
} from "@/lib/validations/demo";
import { requestDemoAccess, type DemoAccessResult } from "@/lib/api/demoApi";
import { ApiError } from "@/lib/api/envelope";

export default function DemoPage() {
  const { toast } = useToast();
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<DemoRequestFormValues>({
    resolver: zodResolver(demoRequestSchema),
    defaultValues: {
      email: "",
      acceptedTerms: false,
      companyName: "",
    },
  });

  const mutation = useMutation<
    DemoAccessResult,
    unknown,
    DemoRequestFormValues
  >({
    mutationFn: requestDemoAccess,
    onSuccess: (_result, vars) => {
      setSubmittedEmail(vars.email);
    },
    onError: (err) => {
      if (err instanceof ApiError && err.status === 429) {
        toast({
          title: "Too many requests",
          description: "Please wait a bit before requesting another demo.",
          variant: "error",
        });
        return;
      }
      if (err instanceof ApiError && err.status === 404) {
        toast({
          title: "Demo unavailable",
          description: "The demo flow is currently disabled.",
          variant: "error",
        });
        return;
      }
      // 400 / 500 fall through to the global toast bridge.
    },
  });

  function onSubmit(values: DemoRequestFormValues): void {
    mutation.mutate(values);
  }

  if (submittedEmail) {
    return (
      <main
        id="main-content"
        className="mx-auto grid min-h-screen max-w-5xl gap-10 px-6 py-12 md:grid-cols-[1fr_22rem]"
      >
        <section>
          <h1 className="text-3xl font-semibold tracking-tight text-fg">
            Check your inbox
          </h1>
          <p className="mt-2 text-sm text-fg-muted">
            If <strong className="text-fg">{submittedEmail}</strong> is eligible,
            we&apos;ve sent your demo credentials. Use them to sign in and
            explore dineOS for the next 7 days.
          </p>
          <ul className="mt-8 space-y-2 text-sm text-fg-muted">
            <li>1. Open the welcome email from dineOS.</li>
            <li>2. Copy the temporary password.</li>
            <li>
              3.{" "}
              <Link
                href="/login"
                className="font-medium text-fg underline underline-offset-2"
              >
                Sign in
              </Link>{" "}
              with the credentials we sent.
            </li>
          </ul>
          <p className="mt-8 text-xs text-fg-subtle">
            Didn&apos;t get an email? Check your spam folder or{" "}
            <button
              type="button"
              className="font-medium text-fg underline underline-offset-2"
              onClick={() => {
                setSubmittedEmail(null);
                mutation.reset();
              }}
            >
              request again
            </button>
            .
          </p>
        </section>

        <DemoSidebar />
      </main>
    );
  }

  return (
    <main
      id="main-content"
      className="mx-auto grid min-h-screen max-w-5xl gap-10 px-6 py-12 md:grid-cols-[1fr_22rem]"
    >
      <section>
        <h1 className="text-3xl font-semibold tracking-tight text-fg">
          Try dineOS in two minutes
        </h1>
        <p className="mt-2 text-sm text-fg-muted">
          Drop your email — we&apos;ll send credentials to a shared demo
          restaurant with sample orders, staff, and a live kitchen board.
        </p>

        <form
          noValidate
          onSubmit={handleSubmit(onSubmit)}
          className="mt-8 grid gap-5"
        >
          <Input
            id="email"
            label="Work email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register("email")}
          />

          {/* Honeypot: visually + a11y hidden, but visible to bots. */}
          <div aria-hidden="true" className="hidden">
            <label htmlFor="company_name">
              Company name (leave this empty)
              <input
                id="company_name"
                type="text"
                tabIndex={-1}
                autoComplete="off"
                {...register("companyName")}
              />
            </label>
          </div>

          <label htmlFor="acceptedTerms" className="flex items-start gap-2.5">
            <input
              id="acceptedTerms"
              type="checkbox"
              className="mt-0.5 h-4 w-4 rounded border-border-strong text-accent focus:ring-accent"
              {...register("acceptedTerms")}
            />
            <span className="text-xs text-fg-muted">
              I understand demo accounts share a single restaurant, expire in
              7 days, and may be reset by dineOS at any time.
            </span>
          </label>
          {errors.acceptedTerms && (
            <span role="alert" className="-mt-3 text-[11px] text-danger">
              {errors.acceptedTerms.message}
            </span>
          )}

          <Button
            type="submit"
            block
            isLoading={mutation.isPending || isSubmitting}
          >
            Email me a demo
          </Button>

          <p className="text-xs text-fg-subtle">
            No card required. We&apos;ll only use this email to send your demo
            credentials.
          </p>
        </form>
      </section>

      <DemoSidebar />
    </main>
  );
}

function DemoSidebar() {
  return (
    <aside className="h-fit rounded-2xl border border-border bg-surface-2 p-6">
      <h2 className="text-lg font-semibold text-fg">What&apos;s in the demo?</h2>
      <ul className="mt-4 space-y-2 text-sm text-fg-muted">
        <li>• Manager-equivalent access</li>
        <li>• Pre-seeded menu, tables, and staff</li>
        <li>• Live kitchen board with sample tickets</li>
        <li>• Reports and shift notes</li>
        <li>• 7-day access — re-request any time</li>
      </ul>
      <p className="mt-6 text-xs text-fg-subtle">
        Ready to run your own restaurant?{" "}
        <Link
          href="/signup"
          className="font-medium text-fg underline underline-offset-2"
        >
          Start on Pro
        </Link>
        .
      </p>
    </aside>
  );
}
