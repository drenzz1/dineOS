import type { OrderStatus } from "@/types/order";

type StatusKey =
  | "new"
  | "progress"
  | "ready"
  | "delivered"
  | "cancelled"
  | "stalled-amber"
  | "stalled-red";

type StatusMeta = { key: StatusKey; label: string };

interface StatusBadgeProps {
  status: OrderStatus;
  solid?: boolean;
  className?: string;
  "data-testid"?: string;
}

const STATUS_MAP: Record<OrderStatus, StatusMeta> = {
  New: { key: "new", label: "New" },
  InProgress: { key: "progress", label: "In progress" },
  Ready: { key: "ready", label: "Ready" },
  Delivered: { key: "delivered", label: "Delivered" },
  Cancelled: { key: "cancelled", label: "Cancelled" },
};

const softClasses: Record<StatusKey, string> = {
  new: "bg-status-new-bg text-status-new-fg border-status-new-border",
  progress: "bg-status-progress-bg text-status-progress-fg border-status-progress-border",
  ready: "bg-status-ready-bg text-status-ready-fg border-status-ready-border",
  delivered: "bg-status-delivered-bg text-status-delivered-fg border-status-delivered-border",
  cancelled: "bg-status-cancelled-bg text-status-cancelled-fg border-status-cancelled-border",
  "stalled-amber": "bg-status-stalled-amber-bg text-status-stalled-amber-fg border-status-stalled-amber-border",
  "stalled-red": "bg-status-stalled-red-bg text-status-stalled-red-fg border-status-stalled-red-border",
};

const solidClasses: Record<StatusKey, string> = {
  new: "bg-status-new-solid",
  progress: "bg-status-progress-solid",
  ready: "bg-status-ready-solid",
  delivered: "bg-status-delivered-solid",
  cancelled: "bg-status-cancelled-solid",
  "stalled-amber": "bg-status-stalled-amber-solid",
  "stalled-red": "bg-status-stalled-red-solid",
};

const dotClasses: Record<StatusKey, string> = {
  new: "bg-status-new-solid",
  progress: "bg-status-progress-solid",
  ready: "bg-status-ready-solid",
  delivered: "bg-status-delivered-solid",
  cancelled: "bg-status-cancelled-solid",
  "stalled-amber": "bg-status-stalled-amber-solid",
  "stalled-red": "bg-status-stalled-red-solid",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function StatusBadge({
  status,
  solid = false,
  className,
  "data-testid": testId,
}: StatusBadgeProps) {
  const meta = STATUS_MAP[status];

  if (solid) {
    return (
      <span
        data-testid={testId}
        className={mergeClasses(
          "inline-flex items-center gap-1.5 h-[22px] px-2 rounded-full text-[11px] font-semibold text-white tracking-[0.01em]",
          solidClasses[meta.key],
          className,
        )}
      >
        <span className="w-1.5 h-1.5 rounded-full bg-white/85" aria-hidden="true" />
        {meta.label}
      </span>
    );
  }

  return (
    <span
      data-testid={testId}
      className={mergeClasses(
        "inline-flex items-center gap-1.5 h-[22px] px-2 rounded-full text-[11px] font-semibold border tracking-[0.005em]",
        softClasses[meta.key],
        className,
      )}
    >
      <span
        className={mergeClasses("w-1.5 h-1.5 rounded-full", dotClasses[meta.key])}
        aria-hidden="true"
      />
      {meta.label}
    </span>
  );
}
