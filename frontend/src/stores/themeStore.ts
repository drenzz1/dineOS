import { create } from "zustand";

type Theme = "light" | "dark";

interface ThemeState {
  theme: Theme;
  toggleTheme: () => void;
  setTheme: (theme: Theme) => void;
}

function getInitialTheme(): Theme {
  if (typeof window === "undefined") return "light";
  const saved = localStorage.getItem("dineos-theme") as Theme | null;
  if (saved === "light" || saved === "dark") return saved;
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function applyTheme(theme: Theme) {
  if (typeof document === "undefined") return;
  document.documentElement.setAttribute("data-theme", theme);
  localStorage.setItem("dineos-theme", theme);
}

export const useThemeStore = create<ThemeState>((set) => ({
  theme: "light",
  toggleTheme: () =>
    set((state) => {
      const next: Theme = state.theme === "light" ? "dark" : "light";
      applyTheme(next);
      return { theme: next };
    }),
  setTheme: (theme: Theme) => {
    applyTheme(theme);
    set({ theme });
  },
}));

export function initTheme() {
  const theme = getInitialTheme();
  applyTheme(theme);
  useThemeStore.setState({ theme });
}
