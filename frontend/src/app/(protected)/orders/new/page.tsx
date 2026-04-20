import OrderWizard from "@/components/orders/OrderWizard";

export default function NewOrderPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-zinc-900">New Order</h1>
      <OrderWizard />
    </div>
  );
}
