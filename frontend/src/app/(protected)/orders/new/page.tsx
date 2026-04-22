import dynamic from "next/dynamic";
import Link from "next/link";

const OrderWizard = dynamic(
  () => import("@/components/orders/OrderWizard"),
  {
    loading: () => (
      <div className="h-96 animate-pulse rounded-lg bg-zinc-100" />
    ),
  }
);

// TODO: restrict to Cashier and Manager roles — blocked by #32
export default function NewOrderPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          href="/orders"
          className="text-sm text-zinc-500 transition-colors hover:text-zinc-900"
        >
          ← Orders
        </Link>
        <span className="text-zinc-300">/</span>
        <h1 className="text-2xl font-semibold text-zinc-900">New Order</h1>
      </div>
      <OrderWizard />
    </div>
  );
}
