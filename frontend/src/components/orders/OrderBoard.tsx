"use client";

import { useState } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import {
  DndContext,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  useDraggable,
} from "@dnd-kit/core";
import type { DragEndEvent } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import { restrictToWindowEdges } from "@dnd-kit/modifiers";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { Illo } from "@/components/ui/Illo";
import { Skeleton } from "@/components/ui/Skeleton";
import OrderCard from "./OrderCard";
import { useOrderBoard } from "@/hooks/useOrderBoard";
import { useTenant } from "@/hooks/useTenant";
import { updateOrderStatus } from "@/lib/api/ordersApi";
import { queryKeys } from "@/lib/api/queryKeys";
import { OrderStatus } from "@/types/order";
import type { Order } from "@/types/order";

const OrderDetailPanel = dynamic(
  () => import("./OrderDetailPanel"),
  { ssr: false, loading: () => null }
);

// ─── Column config ────────────────────────────────────────────────────────────

interface Column {
  status: OrderStatus;
  label: string;
  badgeClass: string;
  countClass: string;
  emptyMessage: string;
}

const COLUMNS: Column[] = [
  {
    status: OrderStatus.New,
    label: "New",
    badgeClass: "bg-status-new-bg text-status-new-fg border-status-new-border",
    countClass: "bg-status-new-solid/10 text-status-new-fg",
    emptyMessage: "No new orders",
  },
  {
    status: OrderStatus.InProgress,
    label: "In progress",
    badgeClass: "bg-status-progress-bg text-status-progress-fg border-status-progress-border",
    countClass: "bg-status-progress-solid/10 text-status-progress-fg",
    emptyMessage: "Nothing in progress",
  },
  {
    status: OrderStatus.Ready,
    label: "Ready",
    badgeClass: "bg-status-ready-bg text-status-ready-fg border-status-ready-border",
    countClass: "bg-status-ready-solid/10 text-status-ready-fg",
    emptyMessage: "Nothing ready yet",
  },
  {
    status: OrderStatus.Delivered,
    label: "Delivered",
    badgeClass: "bg-status-delivered-bg text-status-delivered-fg border-status-delivered-border",
    countClass: "bg-status-delivered-solid/10 text-status-delivered-fg",
    emptyMessage: "No delivered orders",
  },
  {
    status: OrderStatus.Cancelled,
    label: "Cancelled",
    badgeClass: "bg-status-cancelled-bg text-status-cancelled-fg border-status-cancelled-border",
    countClass: "bg-status-cancelled-solid/10 text-status-cancelled-fg",
    emptyMessage: "No cancelled orders",
  },
];

// ─── Skeleton ─────────────────────────────────────────────────────────────────

function BoardSkeleton() {
  return (
    <div
      data-testid="orders-list"
      className="flex flex-col gap-4 lg:grid lg:grid-cols-5 lg:items-start lg:gap-4"
    >
      {COLUMNS.map(({ status, label, badgeClass }) => (
        <section key={status} className="flex flex-col gap-2">
          <div className={`flex items-center justify-between rounded-md border px-3 py-2 ${badgeClass}`}>
            <span className="text-[13px] font-semibold">{label}</span>
            <Skeleton className="h-4 w-6" />
          </div>
          <div className="flex flex-col gap-2">
            {[0, 1].map((i) => (
              <div key={i} className="bg-surface border border-border rounded-md shadow-sm p-4 space-y-2">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-4 w-32" />
                <Skeleton className="h-3 w-16" />
                <Skeleton className="h-5 w-24 rounded-full" />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

// ─── Draggable card ───────────────────────────────────────────────────────────

interface DraggableCardProps {
  order: Order;
  onClick: () => void;
}

function DraggableCard({ order, onClick }: DraggableCardProps) {
  const { attributes, listeners, setNodeRef, isDragging, transform } = useDraggable({
    id: order.id,
    data: { order },
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={{
        transform: CSS.Transform.toString(transform),
        // Disable Card's transform transition while dragging so dnd-kit's
        // per-frame updates aren't smoothed — prevents the size-morph glitch
        transition: isDragging ? "opacity 0ms, box-shadow 0ms" : undefined,
        zIndex: isDragging ? 50 : undefined,
        opacity: isDragging ? 0.5 : 1,
        cursor: isDragging ? "grabbing" : "grab",
        touchAction: "none",
        willChange: isDragging ? "transform" : undefined,
      }}
    >
      <OrderCard order={order} onClick={onClick} />
    </div>
  );
}

// ─── Droppable column ─────────────────────────────────────────────────────────

interface DroppableColumnProps {
  column: Column;
  orders: Order[];
  onCardClick: (order: Order) => void;
}

function DroppableColumn({ column, orders, onCardClick }: DroppableColumnProps) {
  const { status, label, badgeClass, countClass, emptyMessage } = column;
  const { setNodeRef, isOver } = useDroppable({ id: status });

  return (
    <section className="flex flex-col gap-2">
      <div className={`flex items-center justify-between rounded-md border px-3 py-2 ${badgeClass}`}>
        <span className="text-[13px] font-semibold">{label}</span>
        <span className={`dos-num inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-bold ${countClass}`}>
          {orders.length}
        </span>
      </div>

      <div
        ref={setNodeRef}
        className={[
          "flex flex-col gap-2 rounded-md transition-colors duration-150 min-h-[60px] p-0.5",
          isOver ? "bg-accent/8 ring-2 ring-accent/30 ring-inset" : "",
        ].filter(Boolean).join(" ")}
      >
        {orders.length === 0 ? (
          <div className="rounded-md border border-dashed border-border px-3 py-4 text-center">
            <p className="text-[11.5px] text-fg-subtle">{emptyMessage}</p>
          </div>
        ) : (
          orders.map((order) => (
            <DraggableCard
              key={order.id}
              order={order}
              onClick={() => onCardClick(order)}
            />
          ))
        )}
      </div>
    </section>
  );
}

// ─── Board ────────────────────────────────────────────────────────────────────

export default function OrderBoard() {
  const { grouped, isLoading, isError } = useOrderBoard();
  const queryClient = useQueryClient();
  const { tenantId } = useTenant();
  const [selectedOrder, setSelectedOrder] = useState<Order | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      // Require 8px of movement before starting a drag so normal clicks still fire
      activationConstraint: { distance: 8 },
    })
  );

  const { mutate: moveOrder } = useMutation({
    mutationFn: ({ orderId, status }: { orderId: string; status: OrderStatus }) =>
      updateOrderStatus(orderId, status),
    onError: () => {
      // On error, re-fetch to restore server truth
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.list(tenantId) });
    },
  });

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over) return;

    const order = (active.data.current as { order: Order } | undefined)?.order;
    const newStatus = over.id as OrderStatus;

    if (!order || order.status === newStatus) return;

    // Optimistic update — move the card immediately in the UI
    queryClient.setQueryData(
      queryKeys.orders.list(tenantId),
      (old: Order[] | undefined) =>
        (old ?? []).map((o) =>
          o.id === order.id
            ? { ...o, status: newStatus, updatedAt: new Date().toISOString() }
            : o
        )
    );

    moveOrder({ orderId: order.id, status: newStatus });
  }

  const totalOrders = Object.values(grouped).reduce((sum, list) => sum + list.length, 0);

  if (isError) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">Order Board</h1>
        </div>
        <div className="rounded-md border border-border bg-surface py-10">
          <EmptyState
            illustration={<Illo.Ticket />}
            title="Couldn't load orders"
            description="Something went wrong fetching the live board. Please refresh."
          />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.01em] text-fg">Order Board</h1>
          <p className="text-[13px] text-fg-muted mt-0.5">
            {isLoading
              ? "Loading live orders…"
              : `${totalOrders} live ${totalOrders === 1 ? "order" : "orders"} across the floor.`}
          </p>
        </div>
        <Link href="/orders/new">
          <Button
            data-testid="new-order-button"
            leading={
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M12 5v14M5 12h14" />
              </svg>
            }
          >
            New Order
          </Button>
        </Link>
      </div>

      {/* Board */}
      {isLoading ? (
        <BoardSkeleton />
      ) : totalOrders === 0 ? (
        <div className="rounded-md border border-border bg-surface py-6">
          <EmptyState
            illustration={<Illo.Ticket />}
            title="No live orders"
            description="When a new order comes in, it will appear here in real time."
            cta={
              <Link href="/orders/new">
                <Button>Take a new order</Button>
              </Link>
            }
          />
        </div>
      ) : (
        <DndContext
          sensors={sensors}
          modifiers={[restrictToWindowEdges]}
          onDragEnd={handleDragEnd}
        >
          <div
            data-testid="orders-list"
            className="flex flex-col gap-4 lg:grid lg:grid-cols-5 lg:items-start lg:gap-4"
          >
            {COLUMNS.map((column) => (
              <DroppableColumn
                key={column.status}
                column={column}
                orders={grouped[column.status]}
                onCardClick={setSelectedOrder}
              />
            ))}
          </div>

        </DndContext>
      )}

      <OrderDetailPanel
        order={selectedOrder}
        onClose={() => setSelectedOrder(null)}
      />
    </div>
  );
}
