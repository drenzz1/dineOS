import { create } from "zustand";

interface UiState {
  sidebarOpen: boolean;
  activeRoute: string;
  setSidebarOpen: (isOpen: boolean) => void;
  toggleSidebar: () => void;
  setActiveRoute: (route: string) => void;
}

export const useUiStore = create<UiState>((set) => ({
  sidebarOpen: false,
  activeRoute: "/",
  setSidebarOpen: (isOpen: boolean) => set({ sidebarOpen: isOpen }),
  toggleSidebar: () =>
    set((state) => ({
      sidebarOpen: !state.sidebarOpen,
    })),
  setActiveRoute: (route: string) => set({ activeRoute: route }),
}));
