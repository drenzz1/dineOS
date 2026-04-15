import type { OrderStatus } from "@/types/order";

interface StatusBadgeProps {
  status: OrderStatus;
  className?: string;
}

const statusClasses: Record<OrderStatus, string> = {
  New: "bg-blue-100 text-blue-800",
  InProgress: "bg-yellow-100 text-yellow-800",
  Ready: "bg-green-100 text-green-800",
  Delivered: "bg-gray-100 text-gray-700",
  Cancelled: "bg-red-100 text-red-800",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  return (
    <span
      className={mergeClasses(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold",
        statusClasses[status],
        className
      )}
    >
      {status}
    </span>
  );
}
