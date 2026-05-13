"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { queryClient } from "@/lib/queryClient";
import { ToastProvider } from "@/components/ui/ToastProvider";
import { ToastBridge } from "@/components/ui/ToastBridge";

interface ProvidersProps {
  children: ReactNode;
}

export function Providers({ children }: ProvidersProps) {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <ToastBridge />
        {children}
      </ToastProvider>
    </QueryClientProvider>
  );
}
