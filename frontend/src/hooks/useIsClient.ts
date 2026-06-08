import { useSyncExternalStore } from "react";

// Returns false during SSR and on the first client (hydration) render, then true
// after mount. Lets a Client Component defer reading client-only state — e.g. a
// `persist`ed Zustand store that rehydrates synchronously from localStorage — so
// the server output and the first client render agree and React doesn't warn
// about a hydration mismatch (and the UI doesn't flash).
const subscribe = () => () => {};
const getClientSnapshot = () => true;
const getServerSnapshot = () => false;

export function useIsClient(): boolean {
  return useSyncExternalStore(subscribe, getClientSnapshot, getServerSnapshot);
}
