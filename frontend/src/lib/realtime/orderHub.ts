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

function buildConnection(): HubConnection {
  const hubUrl = `${process.env.NEXT_PUBLIC_API_URL ?? "/api"}/hubs/orders`;
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
    startPromise = connection.start().catch((err) => {
      console.warn("[orderHub] connection failed", err);
    }).finally(() => {
      startPromise = null;
    });
  }
  if (startPromise !== null) {
    await startPromise;
  }
  return connection;
}

function releaseConnection(): void {
  refCount -= 1;
  if (refCount <= 0 && connection !== null) {
    connection.stop().catch((err) => console.warn("[orderHub] stop failed", err));
    connection = null;
    startPromise = null;
    refCount = 0;
  }
}

export function useOrderHub(options?: UseOrderHubOptions): void {
  const enabled = options?.enabled ?? true;

  useEffect(() => {
    if (!enabled || typeof window === "undefined") {
      return;
    }

    if (readAccessTokenCookie() === "dev") {
      console.warn("[orderHub] skipping SignalR connection in dev token mode");
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
        conn.start().catch((err) => console.warn("[orderHub] reconnect failed", err));
      }
    };

    acquireConnection()
      .then((c) => {
        if (cancelled) {
          releaseConnection();
          return;
        }
        conn = c;
        c.on("OrderCreated", onCreated);
        c.on("OrderStatusChanged", onStatusChanged);
      })
      .catch((err) => console.warn("[orderHub] start failed", err));

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
