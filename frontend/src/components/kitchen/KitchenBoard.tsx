"use client";

import KitchenTicket from "./KitchenTicket";
import { useKitchenBoard } from "@/hooks/useKitchenBoard";

// ─── Section header ───────────────────────────────────────────────────────────

interface SectionHeadingProps {
  label: string;
  count: number;
  accentClass: string;
}

function SectionHeading({ label, count, accentClass }: SectionHeadingProps) {
  return (
    <div className="mb-4 flex items-center gap-3">
      <h2
        className={`text-sm font-bold uppercase tracking-widest ${accentClass}`}
      >
        {label}
      </h2>
      <span
        className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${accentClass} bg-white/10`}
      >
        {count}
      </span>
      <div className={`h-px flex-1 ${accentClass} opacity-20 bg-current`} />
    </div>
  );
}

// ─── Skeleton ─────────────────────────────────────────────────────────────────

function TicketSkeleton() {
  return (
    <div className="animate-pulse rounded-xl border-2 border-zinc-700 bg-zinc-800 p-5 space-y-4">
      <div className="flex justify-between">
        <div className="space-y-2">
          <div className="h-3 w-20 rounded bg-zinc-700" />
          <div className="h-6 w-36 rounded bg-zinc-700" />
        </div>
        <div className="h-5 w-16 rounded-full bg-zinc-700" />
      </div>
      <div className="space-y-2">
        {[0, 1, 2].map((i) => (
          <div key={i} className="h-4 w-full rounded bg-zinc-700" />
        ))}
      </div>
      <div className="h-11 w-full rounded-md bg-zinc-700" />
    </div>
  );
}

// ─── Board ────────────────────────────────────────────────────────────────────

export default function KitchenBoard() {
  const { newOrders, inProgressOrders, isEmpty, isLoading, isError } =
    useKitchenBoard();

  return (
    // -m-6 cancels the parent layout's p-6 so the dark bg is edge-to-edge
    <div className="-m-6 min-h-screen bg-zinc-900 p-4 md:p-6 lg:p-8">
      {/* Board header */}
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold text-white md:text-3xl lg:text-4xl">
          Kitchen Display
        </h1>
        {isLoading && (
          <span className="text-sm text-zinc-300 animate-pulse">
            Loading…
          </span>
        )}
      </div>

      {/* Error state */}
      {isError && (
        <div className="mb-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3">
          <p className="text-sm font-medium text-red-400">
            Failed to load orders. Retrying every 10 seconds.
          </p>
        </div>
      )}

      {/* Loading skeletons */}
      {isLoading && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <TicketSkeleton key={i} />
          ))}
        </div>
      )}

      {/* Empty state */}
      {!isLoading && isEmpty && (
        <div className="flex flex-col items-center justify-center py-32">
          <p className="text-5xl">✓</p>
          <p className="mt-4 text-2xl font-semibold text-zinc-400">
            Kitchen is clear
          </p>
          <p className="mt-1 text-sm text-zinc-600">
            No active orders right now.
          </p>
        </div>
      )}

      {/* New orders section */}
      {!isLoading && newOrders.length > 0 && (
        <section className="mb-8">
          <SectionHeading
            label="New"
            count={newOrders.length}
            accentClass="text-blue-400"
          />
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {newOrders.map((order) => (
              <KitchenTicket key={order.id} order={order} />
            ))}
          </div>
        </section>
      )}

      {/* In Progress section */}
      {!isLoading && inProgressOrders.length > 0 && (
        <section>
          <SectionHeading
            label="In Progress"
            count={inProgressOrders.length}
            accentClass="text-amber-400"
          />
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {inProgressOrders.map((order) => (
              <KitchenTicket key={order.id} order={order} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
