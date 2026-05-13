"use client";

import { useEffect, useRef } from "react";
import { Card } from "@/components/ui/Card";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

interface OrderCardProps {
  order: Order;
  onClick: () => void;
  onDoubleClick?: () => void;
  actionLabel?: string;
  onAction?: () => void;
  isActionPending?: boolean;
}

function formatElapsed(dateStr: string): string {
  const diffMs = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m ago`;
}

function getStallClass(order: Order): string {
  if (order.status !== OrderStatus.InProgress) return "";
  const minutes = Math.floor(
    (Date.now() - new Date(order.updatedAt).getTime()) / 60_000
  );
  if (minutes > 20)
    return "ring-1 ring-status-stalled-red-border border-status-stalled-red-border animate-pulse-red";
  if (minutes > 10)
    return "ring-1 ring-status-stalled-amber-border border-status-stalled-amber-border animate-pulse-amber";
  return "";
}

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export default function OrderCard({
  order,
  onClick,
  onDoubleClick,
  actionLabel,
  onAction,
  isActionPending,
}: OrderCardProps) {
  const stallClass = getStallClass(order);
  const clickTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (clickTimerRef.current) clearTimeout(clickTimerRef.current);
    };
  }, []);

  function clearClickTimer() {
    if (!clickTimerRef.current) return;
    clearTimeout(clickTimerRef.current);
    clickTimerRef.current = null;
  }

  function handleClick() {
    clearClickTimer();
    clickTimerRef.current = setTimeout(() => {
      clickTimerRef.current = null;
      onClick();
    }, 180);
  }

  function handleDoubleClick() {
    clearClickTimer();
    onDoubleClick?.();
  }

  return (
    <Card
      interactive
      onClick={handleClick}
      onDoubleClick={handleDoubleClick}
      className={mergeClasses(stallClass)}
      role="button"
      tabIndex={0}
      data-testid="order-card"
      data-order-id={order.id}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick();
      }}
    >
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <span className="dos-num text-[11px] font-semibold uppercase text-fg-muted">
            #{order.id.slice(0, 8)}
          </span>
          <span className="text-[11px] text-fg-subtle">
            {formatElapsed(order.createdAt)}
          </span>
        </div>

        <p className="text-[13px] font-medium text-fg">
          {order.orderType === "dine-in"
            ? `Dine-in${order.tableNumber != null ? ` · Table ${order.tableNumber}` : ""}`
            : "Pick-up"}
        </p>

        <p className="text-[11.5px] text-fg-subtle">
          {order.items.length} item{order.items.length !== 1 ? "s" : ""}
        </p>

        {order.notes && (
          <p className="flex items-start gap-1 text-[11.5px] text-fg-muted">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 16 16"
              fill="currentColor"
              className="mt-px h-3 w-3 shrink-0"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                d="M2 4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V4Zm2 1.5a.75.75 0 0 1 .75-.75h6.5a.75.75 0 0 1 0 1.5h-6.5A.75.75 0 0 1 4 5.5Zm0 3a.75.75 0 0 1 .75-.75h6.5a.75.75 0 0 1 0 1.5h-6.5A.75.75 0 0 1 4 8.5Zm0 3a.75.75 0 0 1 .75-.75h4a.75.75 0 0 1 0 1.5h-4A.75.75 0 0 1 4 11.5Z"
                clipRule="evenodd"
              />
            </svg>
            <span className="line-clamp-1">{order.notes}</span>
          </p>
        )}

        <StatusBadge status={order.status} data-testid="order-status-badge" />

        {actionLabel && onAction && (
          <button
            type="button"
            className="mt-1 w-full rounded border border-border bg-surface-2 px-2 py-1.5 text-[11.5px] font-semibold text-fg-muted transition hover:border-border-strong hover:text-fg disabled:cursor-not-allowed disabled:opacity-60"
            disabled={isActionPending}
            onClick={(event) => {
              event.stopPropagation();
              onAction();
            }}
            onDoubleClick={(event) => event.stopPropagation()}
          >
            {isActionPending ? "Updating..." : actionLabel}
          </button>
        )}
      </div>
    </Card>
  );
}
