"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Skeleton } from "@/components/ui/Skeleton";
import { useTenant } from "@/hooks/useTenant";
import { useToast } from "@/hooks/useToast";
import { queryKeys } from "@/lib/api/queryKeys";
import {
  createCheckoutSession,
  createPortalSession,
  getInvoices,
  getSubscription,
} from "@/lib/api/billingApi";
import type { BillingCycle, BillingSubscription, TenantInvoice } from "@/types/billing";

const PLAN_PRICES: Record<BillingCycle, { label: string; sublabel: string }> = {
  Monthly: { label: "€29 / month", sublabel: "Billed monthly · cancel anytime" },
  Annual: { label: "€290 / year", sublabel: "Save ~17% · billed yearly" },
};

function statusBadge(sub: BillingSubscription): { label: string; tone: string } {
  if (sub.plan === "Free") return { label: "Free", tone: "bg-surface-2 text-fg-muted" };
  switch (sub.billingStatus) {
    case "Active":
      return { label: "Active", tone: "bg-status-ready-soft text-status-ready-solid" };
    case "Trialing":
      return { label: "Trial", tone: "bg-status-new-soft text-status-new-solid" };
    case "PastDue":
      return {
        label: "Past due",
        tone: "bg-status-stalled-amber-soft text-status-stalled-amber-solid",
      };
    case "Canceled":
      return {
        label: "Canceled",
        tone: "bg-status-cancelled-soft text-status-cancelled-solid",
      };
    default:
      return { label: sub.billingStatus, tone: "bg-surface-2 text-fg-muted" };
  }
}

function invoiceStatusBadge(status: string): { label: string; tone: string } {
  switch (status) {
    case "paid":
      return { label: "Paid", tone: "bg-status-ready-soft text-status-ready-solid" };
    case "open":
      return { label: "Open", tone: "bg-status-stalled-amber-soft text-status-stalled-amber-solid" };
    default:
      return { label: status, tone: "bg-surface-2 text-fg-muted" };
  }
}

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function formatMoney(amount: number, currency: string): string {
  const symbol = currency.toLowerCase() === "usd" ? "$" : "€";
  return `${symbol}${amount.toFixed(2)}`;
}

export default function BillingCenter() {
  const { tenantId } = useTenant();
  const { toast } = useToast();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [cycle, setCycle] = useState<BillingCycle>("Monthly");

  useEffect(() => {
    const status = searchParams.get("status");
    if (status === "success") {
      toast({
        title: "Subscription activated",
        description: "Welcome to dineOS Pro!",
        variant: "success",
      });
      router.replace("/settings/billing");
    } else if (status === "cancelled") {
      toast({
        title: "Checkout cancelled",
        description: "You can subscribe any time from this page.",
        variant: "warning",
      });
      router.replace("/settings/billing");
    } else if (status === "error") {
      toast({
        title: "Payment failed",
        description: "Your payment could not be processed. Please try again.",
        variant: "error",
      });
      router.replace("/settings/billing");
    }
  }, [searchParams, toast, router]);

  const {
    data: subscription,
    isLoading,
    isError,
  } = useQuery({
    queryKey: queryKeys.billing.subscription(tenantId),
    queryFn: getSubscription,
  });

  const { data: invoices = [], isLoading: invoicesLoading } = useQuery<TenantInvoice[]>({
    queryKey: queryKeys.billing.invoices(tenantId),
    queryFn: getInvoices,
    enabled: !!subscription?.hasStripeSubscription,
  });

  const checkoutMutation = useMutation({
    mutationFn: createCheckoutSession,
    onSuccess: ({ url }) => {
      window.location.href = url;
    },
    onError: (err: Error) => {
      toast({
        title: "Could not start checkout",
        description: err.message,
        variant: "error",
      });
    },
  });

  const portalMutation = useMutation({
    mutationFn: createPortalSession,
    onSuccess: ({ url }) => {
      window.location.href = url;
    },
    onError: (err: Error) => {
      toast({
        title: "Could not open billing portal",
        description: err.message,
        variant: "error",
      });
    },
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (isError || !subscription) {
    return (
      <div className="rounded-lg border border-border bg-surface">
        <EmptyState
          title="Could not load billing"
          description="Refresh the page and try again."
        />
      </div>
    );
  }

  const badge = statusBadge(subscription);
  const showUpgrade = subscription.plan === "Free" || subscription.billingStatus === "Canceled";

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-fg-muted">
              Current plan
            </p>
            <div className="mt-1 flex items-center gap-2">
              <h2 className="font-mono text-2xl font-semibold tracking-[-0.02em] text-fg">
                {subscription.plan}
              </h2>
              <span
                className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${badge.tone}`}
              >
                {badge.label}
              </span>
            </div>
            {subscription.billingCycle && (
              <p className="mt-1 text-xs text-fg-muted">
                {subscription.billingCycle} cycle · renews {formatDate(subscription.currentPeriodEnd)}
              </p>
            )}
          </div>

          {subscription.hasStripeSubscription && (
            <Button
              variant="secondary"
              onClick={() => portalMutation.mutate()}
              isLoading={portalMutation.isPending}
            >
              Manage billing
            </Button>
          )}
        </div>
      </section>

      {showUpgrade && (
        <section className="rounded-lg border border-border bg-surface shadow-sm">
          <div className="border-b border-border px-5 py-4">
            <h2 className="text-base font-semibold text-fg">Upgrade to Pro</h2>
            <p className="text-[12px] text-fg-muted">
              Unlock full analytics, priority support, and unlimited menu categories.
            </p>
          </div>

          <div className="space-y-4 p-5">
            <div className="grid gap-3 sm:grid-cols-2">
              {(["Monthly", "Annual"] as const).map((option) => {
                const isSelected = cycle === option;
                const price = PLAN_PRICES[option];
                return (
                  <button
                    key={option}
                    type="button"
                    onClick={() => setCycle(option)}
                    className={`rounded-md border px-4 py-3 text-left transition ${
                      isSelected
                        ? "border-accent bg-accent-soft"
                        : "border-border bg-surface hover:border-fg-muted"
                    }`}
                  >
                    <p className="text-sm font-semibold text-fg">{option}</p>
                    <p className="mt-1 font-mono text-lg font-semibold tracking-[-0.02em] text-fg">
                      {price.label}
                    </p>
                    <p className="mt-0.5 text-[11px] text-fg-muted">
                      {price.sublabel}
                    </p>
                  </button>
                );
              })}
            </div>

            <Button
              className="w-full"
              onClick={() => checkoutMutation.mutate(cycle)}
              isLoading={checkoutMutation.isPending}
            >
              Continue to checkout · {PLAN_PRICES[cycle].label}
            </Button>
            <p className="text-center text-[11px] text-fg-muted">
              You will be redirected to Stripe to complete payment.
            </p>
          </div>
        </section>
      )}

      {subscription.hasStripeSubscription && (
        <section className="rounded-lg border border-border bg-surface shadow-sm">
          <div className="border-b border-border px-5 py-4">
            <h2 className="text-base font-semibold text-fg">Invoice history</h2>
            <p className="text-[12px] text-fg-muted">
              Your past dineOS subscription invoices.
            </p>
          </div>
          {invoicesLoading ? (
            <div className="space-y-3 p-5">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : invoices.length === 0 ? (
            <EmptyState
              title="No invoices yet"
              description="Your invoices will appear here once a payment has been processed."
            />
          ) : (
            <div className="divide-y divide-border">
              {invoices.map((invoice) => {
                const invBadge = invoiceStatusBadge(invoice.status);
                return (
                  <div
                    key={invoice.id}
                    className="flex items-center justify-between gap-3 px-5 py-3"
                  >
                    <div className="flex min-w-0 items-center gap-3">
                      <span
                        className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${invBadge.tone}`}
                      >
                        {invBadge.label}
                      </span>
                      <span className="text-sm font-medium text-fg">
                        {formatMoney(invoice.amount, invoice.currency)}
                      </span>
                      <span className="shrink-0 text-xs text-fg-muted">
                        {formatDate(invoice.paidAt ?? invoice.createdAt)}
                      </span>
                    </div>
                    {invoice.hostedInvoiceUrl && (
                      <a
                        href={invoice.hostedInvoiceUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="shrink-0 text-xs font-medium text-accent hover:underline"
                      >
                        View
                      </a>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </section>
      )}
    </div>
  );
}
