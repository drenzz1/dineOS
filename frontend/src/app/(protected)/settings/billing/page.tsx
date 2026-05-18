import { Suspense } from "react";
import BillingCenter from "@/components/billing/BillingCenter";

export default function BillingPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
          Billing
        </h1>
        <p className="mt-0.5 text-[13px] text-fg-muted">
          Manage your dineOS subscription, change cycle, or update payment method.
        </p>
      </div>

      <Suspense>
        <BillingCenter />
      </Suspense>
    </div>
  );
}
