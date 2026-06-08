// Resolves the SignalR hub URL. The hub is mapped at the top-level, unversioned
// /hubs/orders on the backend (NOT under /api), so we strip a trailing /api from
// the REST base. The resulting relative "/hubs/orders" is proxied to the backend
// by the next.config `/hubs/:path*` rewrite; an absolute base keeps its origin.
// This avoids the broken "/api/hubs/orders" path, which the backend does not map.
export function resolveOrderHubUrl(): string {
  const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "/api";
  const origin = apiBase.replace(/\/api\/?$/, "");
  return `${origin}/hubs/orders`;
}
