import apiClient from "@/lib/api/apiClient";
import { type ApiResponse, unwrap, toApiError } from "@/lib/api/envelope";
import type {
  BillingCycle,
  BillingSubscription,
  StripeRedirect,
  TenantInvoice,
} from "@/types/billing";

interface BillingSubscriptionDto {
  plan: string;
  billingStatus: string;
  billingCycle: string | null;
  currentPeriodEnd: string | null;
  hasStripeSubscription: boolean;
}

interface StripeRedirectDto {
  url: string;
}

function mapSubscription(dto: BillingSubscriptionDto): BillingSubscription {
  return {
    plan: dto.plan as BillingSubscription["plan"],
    billingStatus: dto.billingStatus as BillingSubscription["billingStatus"],
    billingCycle: (dto.billingCycle as BillingCycle | null) ?? null,
    currentPeriodEnd: dto.currentPeriodEnd ?? null,
    hasStripeSubscription: dto.hasStripeSubscription,
  };
}

export async function getSubscription(): Promise<BillingSubscription> {
  try {
    const res = await apiClient.get<ApiResponse<BillingSubscriptionDto>>(
      "/v1/billing/subscription"
    );
    return mapSubscription(unwrap(res));
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createCheckoutSession(
  cycle: BillingCycle
): Promise<StripeRedirect> {
  try {
    const res = await apiClient.post<ApiResponse<StripeRedirectDto>>(
      "/v1/billing/checkout-session",
      { cycle }
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function createPortalSession(): Promise<StripeRedirect> {
  try {
    const res = await apiClient.post<ApiResponse<StripeRedirectDto>>(
      "/v1/billing/portal-session"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}

export async function getInvoices(): Promise<TenantInvoice[]> {
  try {
    const res = await apiClient.get<ApiResponse<TenantInvoice[]>>(
      "/v1/billing/invoices"
    );
    return unwrap(res);
  } catch (error) {
    throw toApiError(error);
  }
}
