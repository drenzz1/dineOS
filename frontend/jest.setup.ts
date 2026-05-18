import '@testing-library/jest-dom';
import { TextEncoder, TextDecoder } from 'util';
Object.assign(global, { TextEncoder, TextDecoder });

jest.mock("@microsoft/signalr", () => {
  const fakeConn = {
    state: "Disconnected",
    start: jest.fn().mockResolvedValue(undefined),
    stop: jest.fn().mockResolvedValue(undefined),
    on: jest.fn(),
    off: jest.fn(),
  };
  return {
    HubConnectionBuilder: jest.fn().mockImplementation(() => ({
      withUrl: jest.fn().mockReturnThis(),
      withAutomaticReconnect: jest.fn().mockReturnThis(),
      configureLogging: jest.fn().mockReturnThis(),
      build: jest.fn(() => fakeConn),
    })),
    HubConnectionState: { Disconnected: "Disconnected", Connected: "Connected", Connecting: "Connecting", Reconnecting: "Reconnecting", Disconnecting: "Disconnecting" },
    LogLevel: { Trace: 0, Debug: 1, Information: 2, Warning: 3, Error: 4, Critical: 5, None: 6 },
  };
});
