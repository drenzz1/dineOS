// SignalR client for /hubs/orders — invalidates TanStack Query caches on order events
import { useEffect } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { queryClient } from "@/lib/queryClient";
import { queryKeys } from "@/lib/api/queryKeys";
import { resolveOrderHubUrl } from "@/lib/realtime/hubUrl";

export interface OrderCreatedEvent {
  orderId: number;
  tenantId: number;
  orderType: string;
  tableNumber: number | null;
  status: string;
  total: number;
  notes: string | null;
  createdAt: string;
  items: Array<{ id: number; name: string; quantity: number; unitPrice: number; notes: string | null }>;
}

export interface OrderStatusChangedEvent {
  orderId: number;
  tenantId: number;
  oldStatus: string;
  newStatus: string;
  changedAt: string;
}

export interface UseOrderHubOptions {
  enabled?: boolean;
}

let connection: HubConnection | null = null;
let refCount = 0;
let startPromise: Promise<void> | null = null;

function readAccessTokenCookie(): string {
  if (typeof document === "undefined") {
    return "";
  }
  const cookie = document.cookie
    .split("; ")
    .find((row) => row.startsWith("access_token="));
  return cookie ? decodeURIComponent(cookie.split("=")[1] ?? "") : "";
}

// React StrictMode (dev) mounts→unmounts→mounts effects, and navigating away
// can stop a connection mid-handshake. SignalR then rejects the in-flight
// start()/stop() with one of these benign messages. They are expected and
// self-healing (we fall back to TanStack Query polling + automatic reconnect),
// so we don't surface them as warnings.
function isBenignHubError(err: unknown): boolean {
  const message = err instanceof Error ? err.message : String(err ?? "");
  return (
    message.includes("stopped during negotiation") ||
    message.includes("stopped before the hub handshake") ||
    message.includes("connection was stopped") ||
    message.includes("connection being closed") ||
    message.includes("connection is stopping")
  );
}

function buildConnection(): HubConnection {
  const hubUrl = resolveOrderHubUrl();
  return new HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => readAccessTokenCookie() })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();
}

async function acquireConnection(): Promise<HubConnection> {
  refCount += 1;
  if (connection === null) {
    connection = buildConnection();
  }
  if (startPromise === null && connection.state === HubConnectionState.Disconnected) {
    const pending = connection.start().catch((err) => {
      if (!isBenignHubError(err)) {
        console.warn("[orderHub] connection failed", err);
      }
    });
    startPromise = pending;
    // Only clear if it's still ours — a StrictMode remount may have started a
    // fresh connection and reassigned startPromise in the meantime.
    pending.finally(() => {
      if (startPromise === pending) {
        startPromise = null;
      }
    });
  }
  if (startPromise !== null) {
    await startPromise;
  }
  return connection;
}

function releaseConnection(): void {
  refCount -= 1;
  if (refCount > 0 || connection === null) {
    return;
  }
  const conn = connection;
  const pendingStart = startPromise;
  connection = null;
  startPromise = null;
  refCount = 0;
  // Wait for any in-flight start() to settle before stopping, so we don't abort
  // the negotiation handshake (the benign "stopped during negotiation" error).
  Promise.resolve(pendingStart)
    .then(() => conn.stop())
    .catch((err) => {
      if (!isBenignHubError(err)) {
        console.warn("[orderHub] stop failed", err);
      }
    });
}

export function useOrderHub(options?: UseOrderHubOptions): void {
  const enabled = options?.enabled ?? true;

  useEffect(() => {
    if (!enabled || typeof window === "undefined") {
      return;
    }

    let cancelled = false;
    let conn: HubConnection | null = null;

    const onCreated = (_event: OrderCreatedEvent) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    };

    const onStatusChanged = (_event: OrderStatusChangedEvent) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
    };

    const onVisibilityChange = () => {
      if (document.visibilityState !== "visible") {
        return;
      }
      queryClient.invalidateQueries({ queryKey: queryKeys.orders.all });
      if (conn && conn.state === HubConnectionState.Disconnected) {
        conn.start().catch((err) => {
          if (!isBenignHubError(err)) {
            console.warn("[orderHub] reconnect failed", err);
          }
        });
      }
    };

    acquireConnection()
      .then((c) => {
        // The effect's cleanup is the single owner of this acquire's release —
        // don't release here too, or a StrictMode mount→unmount→mount would
        // double-release and tear down the connection the remount just built.
        if (cancelled) {
          return;
        }
        conn = c;
        c.on("OrderCreated", onCreated);
        c.on("OrderStatusChanged", onStatusChanged);
      })
      .catch((err) => {
        if (!isBenignHubError(err)) {
          console.warn("[orderHub] start failed", err);
        }
      });

    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", onVisibilityChange);
      if (conn) {
        conn.off("OrderCreated", onCreated);
        conn.off("OrderStatusChanged", onStatusChanged);
      }
      releaseConnection();
    };
  }, [enabled]);
}
