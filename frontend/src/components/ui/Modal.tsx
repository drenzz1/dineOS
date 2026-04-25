"use client";

import { useEffect } from "react";
import type { ReactNode } from "react";
import { useFocusTrap } from "@/hooks/useFocusTrap";

type ModalWidth = "sm" | "md" | "lg" | "xl";

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  width?: ModalWidth;
}

const widthClasses: Record<ModalWidth, string> = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-lg",
  xl: "max-w-2xl",
};

function mergeClasses(...classes: Array<string | undefined | false>): string {
  return classes.filter(Boolean).join(" ");
}

export function Modal({
  isOpen,
  onClose,
  title,
  children,
  footer,
  width = "lg",
}: ModalProps) {
  const dialogRef = useFocusTrap(isOpen);

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    if (isOpen) {
      document.addEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "hidden";
    }
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "";
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto px-4 py-8 md:py-16 animate-fade-in">
      <div
        data-testid="modal-overlay"
        className="fixed inset-0 bg-warm-1000/40 backdrop-blur-[4px]"
        aria-hidden="true"
        onClick={onClose}
      />
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        className={mergeClasses(
          "relative z-10 w-full bg-surface border border-border rounded-lg shadow-xl overflow-hidden animate-pop",
          widthClasses[width],
        )}
      >
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-border">
          <h2
            id="modal-title"
            className="text-[15px] font-semibold tracking-[-0.01em] text-fg"
          >
            {title}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="flex h-7 w-7 items-center justify-center rounded-sm text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg"
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
        <div className="px-5 py-4 text-fg">{children}</div>
        {footer && (
          <div className="flex justify-end gap-2 px-5 py-3 border-t border-border bg-surface-2">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
