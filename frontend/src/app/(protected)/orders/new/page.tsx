import Link from "next/link";
import OrderQuickCreate from "@/components/orders/OrderQuickCreate";

// TODO: restrict to Cashier and Manager roles — blocked by #32
export default function NewOrderPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          href="/orders"
          className="text-sm text-fg-muted transition-colors hover:text-fg"
        >
          Back to orders
        </Link>
        <span className="text-fg-subtle">/</span>
        <h1 className="text-2xl font-semibold tracking-[-0.02em] text-fg">
          New Order
        </h1>
      </div>
      <OrderQuickCreate />
    </div>
  );
}
