import PaymentCenter from "@/components/payments/PaymentCenter";

export default function PaymentsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">
          Payments
        </h1>
        <p className="mt-0.5 text-[13px] text-fg-muted">
          Settle open checks from one cashier-focused screen.
        </p>
      </div>

      <PaymentCenter />
    </div>
  );
}
