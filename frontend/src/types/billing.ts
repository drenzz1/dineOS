export type BillingCycle = "Monthly" | "Annual";

export type SubscriptionPlan = "Free" | "Pro";

export type BillingStatus =
  | "None"
  | "Trialing"
  | "Active"
  | "PastDue"
  | "Canceled"
  | "Incomplete";

export interface BillingSubscription {
  plan: SubscriptionPlan;
  billingStatus: BillingStatus;
  billingCycle: BillingCycle | null;
  currentPeriodEnd: string | null;
  hasStripeSubscription: boolean;
}

export interface StripeRedirect {
  url: string;
}
