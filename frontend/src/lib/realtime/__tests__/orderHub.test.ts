import { renderHook, act } from "@testing-library/react";

type Handler = (arg: unknown) => void;

const handlers: Record<string, Handler[]> = {};

const fakeConn = {
  state: "Disconnected" as string,
  start: jest.fn().mockResolvedValue(undefined),
  stop: jest.fn().mockResolvedValue(undefined),
  on: jest.fn((evt: string, cb: Handler) => {
    (handlers[evt] ||= []).push(cb);
  }),
  off: jest.fn((evt: string, cb: Handler) => {
    handlers[evt] = (handlers[evt] || []).filter((h) => h !== cb);
  }),
};

const fireEvent = (evt: string, payload: unknown) => {
  (handlers[evt] || []).forEach((h) => h(payload));
};

jest.mock("@microsoft/signalr", () => {
  const builderInstance = {
    withUrl: jest.fn().mockReturnThis(),
    withAutomaticReconnect: jest.fn().mockReturnThis(),
    configureLogging: jest.fn().mockReturnThis(),
    build: jest.fn(() => fakeConn),
  };
  return {
    HubConnectionBuilder: jest.fn(() => builderInstance),
    HubConnectionState: { Disconnected: "Disconnected", Connected: "Connected" },
    LogLevel: { Warning: 3 },
  };
});

jest.mock("@/lib/queryClient", () => ({
  queryClient: {
    invalidateQueries: jest.fn(),
  },
}));

import { queryClient } from "@/lib/queryClient";
import { queryKeys } from "@/lib/api/queryKeys";
import { useOrderHub } from "@/lib/realtime/orderHub";
import { HubConnectionBuilder } from "@microsoft/signalr";

beforeEach(() => {
  jest.clearAllMocks();
  for (const key of Object.keys(handlers)) {
    delete handlers[key];
  }
  fakeConn.state = "Disconnected";
  fakeConn.start.mockResolvedValue(undefined);
  fakeConn.stop.mockResolvedValue(undefined);
});

describe("useOrderHub", () => {
  it("mounts and starts a single connection", async () => {
    const { unmount } = renderHook(() => useOrderHub());

    await act(async () => {
      await Promise.resolve();
    });

    expect(HubConnectionBuilder).toHaveBeenCalledTimes(1);
    expect(fakeConn.start).toHaveBeenCalledTimes(1);
    expect(fakeConn.on).toHaveBeenCalledWith("OrderCreated", expect.any(Function));
    expect(fakeConn.on).toHaveBeenCalledWith("OrderStatusChanged", expect.any(Function));

    unmount();
  });

  it("invalidates orders on OrderCreated", async () => {
    const { unmount } = renderHook(() => useOrderHub());

    await act(async () => {
      await Promise.resolve();
    });

    await act(async () => {
      fireEvent("OrderCreated", {
        orderId: 1,
        tenantId: 1,
        orderType: "dine-in",
        tableNumber: 3,
        status: "New",
        total: 12.99,
        notes: null,
        createdAt: new Date().toISOString(),
        items: [],
      });
    });

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({
      queryKey: queryKeys.orders.all,
    });

    unmount();
  });

  it("invalidates orders on OrderStatusChanged", async () => {
    const { unmount } = renderHook(() => useOrderHub());

    await act(async () => {
      await Promise.resolve();
    });

    await act(async () => {
      fireEvent("OrderStatusChanged", {
        orderId: 1,
        tenantId: 1,
        oldStatus: "New",
        newStatus: "InProgress",
        changedAt: new Date().toISOString(),
      });
    });

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({
      queryKey: queryKeys.orders.all,
    });

    unmount();
  });

  it("shares a single connection across multiple hook instances and stops only when all unmount", async () => {
    const { unmount: unmount1 } = renderHook(() => useOrderHub());
    const { unmount: unmount2 } = renderHook(() => useOrderHub());

    await act(async () => {
      await Promise.resolve();
    });

    expect(fakeConn.start).toHaveBeenCalledTimes(1);

    unmount1();
    expect(fakeConn.stop).not.toHaveBeenCalled();

    unmount2();
    // stop() is deferred until any in-flight start() settles (so we never abort
    // negotiation), so flush microtasks before asserting it ran.
    await act(async () => {
      await Promise.resolve();
    });
    expect(fakeConn.stop).toHaveBeenCalledTimes(1);
  });
});
