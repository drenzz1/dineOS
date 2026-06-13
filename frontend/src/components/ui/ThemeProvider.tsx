"use client";

import { useEffect } from "react";
import type { ReactNode } from "react";
import { initTheme } from "@/stores/themeStore";

interface ThemeProviderProps {
  children: ReactNode;
}

export function ThemeProvider({ children }: ThemeProviderProps) {
  useEffect(() => {
    initTheme();
  }, []);

  return <>{children}</>;
}
