"use client";

import { useState } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  useDraggable,
} from "@dnd-kit/core";
import type { DragEndEvent, DragStartEvent } from "@dnd-kit/core";
import { restrictToWindowEdges, snapCenterToCursor } from "@dnd-kit/modifiers";
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

// ─── Status progression ───────────────────────────────────────────────────────

const NEXT_STATUS: Partial<Record<OrderStatus, OrderStatus>> = {
  [OrderStatus.New]: OrderStatus.InProgress,
  [OrderStatus.InProgress]: OrderStatus.Ready,
  [OrderStatus.Ready]: OrderStatus.Delivered,
};

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
  onDoubleClick: () => void;
}

function DraggableCard({ order, onClick, onDoubleClick }: DraggableCardProps) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: order.id,
    data: { order },
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={{
        opacity: isDragging ? 0 : 1,
        cursor: "grab",
        touchAction: "none",
      }}
    >
      <OrderCard order={order} onClick={onClick} onDoubleClick={onDoubleClick} />
    </div>
  );
}

// ─── Droppable column ─────────────────────────────────────────────────────────

interface DroppableColumnProps {
  column: Column;
  orders: Order[];
  onCardClick: (order: Order) => void;
  onCardDoubleClick: (order: Order) => void;
}

function DroppableColumn({ column, orders, onCardClick, onCardDoubleClick }: DroppableColumnProps) {
  const { status, label, badgeClass, countClass, emptyMessage } = column;
  const { setNodeRef, isOver } = useDroppable({ id: status });

  return (
    <section className="flex flex-col gap-2" aria-label={`${label} orders`}>
      <div className={`flex items-center justify-between rounded-md border px-3 py-2 ${badgeClass}`}>
        <span className="text-[13px] font-semibold">{label}</span>
        <span className={`dos-num inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-bold ${countClass}`}>
          {orders.length}
        </span>
      </div>

      <div
        ref={setNodeRef}
        className={[
          "flex flex-col gap-2 rounded-md min-h-[60px]",
          // Use outline (not ring) — outline doesn't affect box model so no layout shift
          isOver ? "bg-accent/8 outline outline-2 outline-accent/40 outline-offset-[-2px]" : "",
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
              onDoubleClick={() => onCardDoubleClick(order)}
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
  const [activeOrder, setActiveOrder] = useState<Order | null>(null);
  const [overlayWidth, setOverlayWidth] = useState<number | undefined>(undefined);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      // 8px threshold so normal clicks still open the detail panel
      activationConstraint: { distance: 8 },
    })
  );

  const { mutate: moveOrder } = useMutation({
    mutationFn: ({ orderId, status }: { orderId: string; status: OrderStatus }) =>
      updateOrderStatus(orderId, status),
    onError: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.list(tenantId) });
    },
  });

  function handleDragStart(event: DragStartEvent) {
    const order = (event.active.data.current as { order: Order } | undefined)?.order;
    setActiveOrder(order ?? null);
    // Capture the card's current pixel width so the overlay renders at the same size
    const width = event.active.rect.current.initial?.width;
    setOverlayWidth(width ?? undefined);
  }

  function handleDoubleClick(order: Order) {
    const newStatus = NEXT_STATUS[order.status];
    if (!newStatus) return;

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

  function handleDragEnd(event: DragEndEvent) {
    setActiveOrder(null);
    setOverlayWidth(undefined);

    const { active, over } = event;
    if (!over) return;

    const order = (active.data.current as { order: Order } | undefined)?.order;
    const newStatus = over.id as OrderStatus;
    if (!order || order.status === newStatus) return;

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
          modifiers={[snapCenterToCursor]}
          onDragStart={handleDragStart}
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
                onCardDoubleClick={handleDoubleClick}
              />
            ))}
          </div>

          {/* Overlay rendered at body level — fixed width from measured card so it never resizes */}
          <DragOverlay modifiers={[restrictToWindowEdges]} dropAnimation={null}>
            {activeOrder ? (
              <div style={{ width: overlayWidth, cursor: "grabbing" }}>
                <OrderCard order={activeOrder} onClick={() => {}} />
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>
      )}

      <OrderDetailPanel
        order={selectedOrder}
        onClose={() => setSelectedOrder(null)}
      />
    </div>
  );
}
