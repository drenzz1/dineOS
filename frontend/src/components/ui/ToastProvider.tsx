"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";

export type ToastVariant = "success" | "error" | "info" | "warning";

export interface ToastOptions {
  title: string;
  description?: string;
  variant?: ToastVariant;
  duration?: number;
  testId?: string;
}

interface ToastRecord extends Required<Omit<ToastOptions, "description" | "testId">> {
  id: number;
  description?: string;
  testId?: string;
}

interface ToastContextValue {
  toast: (opts: ToastOptions) => number;
  dismiss: (id: number) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const variantStyles: Record<ToastVariant, string> = {
  success: "border-l-4 border-status-ready-solid",
  error: "border-l-4 border-status-cancelled-solid",
  info: "border-l-4 border-status-new-solid",
  warning: "border-l-4 border-status-stalled-amber-solid",
};

const variantIconColor: Record<ToastVariant, string> = {
  success: "text-status-ready-solid",
  error: "text-status-cancelled-solid",
  info: "text-status-new-solid",
  warning: "text-status-stalled-amber-solid",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

function ToastIcon({ variant }: { variant: ToastVariant }) {
  const shared = {
    width: 16,
    height: 16,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    "aria-hidden": true,
  };
  const color = variantIconColor[variant];
  if (variant === "success")
    return (
      <svg {...shared} className={color}>
        <path d="m5 12 5 5L20 7" />
      </svg>
    );
  if (variant === "error")
    return (
      <svg {...shared} className={color}>
        <circle cx="12" cy="12" r="10" />
        <path d="M15 9l-6 6M9 9l6 6" />
      </svg>
    );
  if (variant === "warning")
    return (
      <svg {...shared} className={color}>
        <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
        <path d="M12 9v4M12 17h.01" />
      </svg>
    );
  return (
    <svg {...shared} className={color}>
      <circle cx="12" cy="12" r="10" />
      <path d="M12 8v4M12 16h.01" />
    </svg>
  );
}

interface ToastProviderProps {
  children: ReactNode;
}

export function ToastProvider({ children }: ToastProviderProps) {
  const [toasts, setToasts] = useState<ToastRecord[]>([]);
  const idRef = useRef(0);
  const timersRef = useRef(new Map<number, ReturnType<typeof setTimeout>>());

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const timer = timersRef.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timersRef.current.delete(id);
    }
  }, []);

  const toast = useCallback(
    ({ title, description, variant = "info", duration = 4000, testId }: ToastOptions) => {
      const id = ++idRef.current;
      const record: ToastRecord = { id, title, description, variant, duration, testId };
      setToasts((prev) => [...prev, record]);
      if (duration > 0) {
        const timer = setTimeout(() => dismiss(id), duration);
        timersRef.current.set(id, timer);
      }
      return id;
    },
    [dismiss],
  );

  useEffect(() => {
    const timers = timersRef.current;
    return () => {
      timers.forEach((t) => clearTimeout(t));
      timers.clear();
    };
  }, []);

  const value = useMemo<ToastContextValue>(() => ({ toast, dismiss }), [toast, dismiss]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div
        aria-live="polite"
        aria-atomic="false"
        className="fixed bottom-4 right-4 z-[120] flex flex-col gap-2 max-w-[360px] w-[calc(100vw-2rem)] pointer-events-none"
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            role="status"
            data-testid={t.testId}
            className={mergeClasses(
              "pointer-events-auto flex items-start gap-3 bg-surface border border-border rounded-md shadow-lg px-3.5 py-3 animate-fade-up",
              variantStyles[t.variant],
            )}
          >
            <ToastIcon variant={t.variant} />
            <div className="flex-1 min-w-0">
              <div className="text-[13px] font-semibold tracking-[-0.005em] text-fg">
                {t.title}
              </div>
              {t.description && (
                <div className="text-xs text-fg-muted mt-0.5 leading-[1.45]">
                  {t.description}
                </div>
              )}
            </div>
            <button
              type="button"
              onClick={() => dismiss(t.id)}
              aria-label="Dismiss notification"
              className="text-fg-subtle hover:text-fg transition-colors -m-1 p-1 rounded-xs"
            >
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToastContext() {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return ctx;
}
