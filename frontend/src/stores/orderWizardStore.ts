import { create } from "zustand";

type WizardStep = 1 | 2 | 3;

interface OrderWizardState {
  step: WizardStep;
  nextStep: () => void;
  prevStep: () => void;
  reset: () => void;
}

export const useOrderWizardStore = create<OrderWizardState>((set) => ({
  step: 1,
  nextStep: () =>
    set((state) => ({ step: Math.min(state.step + 1, 3) as WizardStep })),
  prevStep: () =>
    set((state) => ({ step: Math.max(state.step - 1, 1) as WizardStep })),
  reset: () => set({ step: 1 }),
}));
