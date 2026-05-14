"use client";

import { useEffect } from "react";
import { useToast } from "@/hooks/useToast";
import { registerToastFn } from "@/lib/api/errorToast";

export function ToastBridge() {
  const { toast } = useToast();

  useEffect(() => {
    registerToastFn(toast);
  }, [toast]);

  return null;
}
