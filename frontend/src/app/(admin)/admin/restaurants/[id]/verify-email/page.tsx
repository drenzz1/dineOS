"use client";

import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { emailVerificationSchema } from "@/lib/validations/restaurant";
import type { EmailVerificationFormValues } from "@/lib/validations/restaurant";
import { confirmEmailVerification } from "@/lib/api/restaurantApi";
import { type ApiError } from "@/lib/api/envelope";
import { Button } from "@/components/ui/Button";
import { useToast } from "@/hooks/useToast";

const INPUT =
  "block w-full rounded-md border border-zinc-300 px-3 py-2 text-sm text-zinc-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500";

export default function VerifyEmailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { toast } = useToast();

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<EmailVerificationFormValues>({
    resolver: zodResolver(emailVerificationSchema),
  });

  const { mutate, isPending } = useMutation({
    mutationFn: (data: EmailVerificationFormValues) =>
      confirmEmailVerification(Number(id), data.code),
    onSuccess: () => {
      toast({ title: "Email verified successfully!", variant: "success" });
      router.push(`/admin/restaurants/${id}`);
    },
    onError: (error) => {
      const apiError = error as ApiError;
      if (apiError.status === 429) {
        toast({ title: "Too many attempts. Please wait.", variant: "error" });
      } else if (apiError.status === 400 || apiError.status === 422) {
        setError("code", { message: apiError.errors[0] ?? apiError.error });
      } else {
        toast({ title: apiError.error, variant: "error" });
      }
    },
  });

  return (
    <form
      onSubmit={handleSubmit((data) => mutate(data))}
      noValidate
      className="mx-auto max-w-sm space-y-6"
    >
      <div>
        <h1 className="text-2xl font-semibold text-zinc-900">Verify Email</h1>
        <p className="mt-1 text-sm text-zinc-500">
          Enter the 6-digit code sent to the restaurant owner&apos;s email.
        </p>
      </div>

      <div className="space-y-1">
        <label htmlFor="code" className="block text-sm font-medium text-zinc-700">
          Verification code
        </label>
        <input
          id="code"
          type="text"
          inputMode="numeric"
          pattern="[0-9]*"
          maxLength={6}
          autoComplete="one-time-code"
          {...register("code")}
          className={`${INPUT} text-center text-2xl tracking-[0.5em]`}
          aria-invalid={!!errors.code}
          aria-describedby={errors.code ? "code-error" : undefined}
        />
        {errors.code && (
          <p id="code-error" className="text-sm text-red-600" role="alert">
            {errors.code.message}
          </p>
        )}
      </div>

      <div className="flex items-center justify-end gap-3 border-t border-zinc-200 pt-4">
        <Button
          type="button"
          variant="secondary"
          onClick={() => router.push(`/admin/restaurants/${id}`)}
        >
          Cancel
        </Button>
        <Button type="submit" isLoading={isPending}>
          Verify Email
        </Button>
      </div>
    </form>
  );
}
