import { NextRequest, NextResponse } from "next/server";
import { resolveRequestRole } from "@/lib/auth/routeRole";

const CASHIER_ALLOWED = ["/orders", "/payments", "/kitchen", "/shifts"];
const KITCHEN_STAFF_ALLOWED = ["/kitchen", "/shifts"];
const PUBLIC_PATHS = [
  "/",
  "/login",
  "/auth/callback",
  "/signup",
  "/signup/success",
  "/signup/cancelled",
  "/demo",
  "/first-login",
  // Always reachable so the "you don't have access" page can render without the
  // role check below redirecting it back onto itself.
  "/forbidden",
];

// Prefix-matched public routes (the path itself or anything nested under it).
// `/info/*` serves the public marketing/legal/resources pages linked from the footer.
const PUBLIC_PREFIXES = ["/info"];

function isAllowed(pathname: string, allowed: string[]): boolean {
  return allowed.some(
    (prefix) => pathname === prefix || pathname.startsWith(prefix + "/")
  );
}

function isAdminPath(pathname: string): boolean {
  return pathname === "/admin" || pathname.startsWith("/admin/");
}

function redirectTo(destination: string, request: NextRequest): NextResponse {
  return NextResponse.redirect(new URL(destination, request.url));
}

// Send an authenticated-but-unauthorized user to a friendly "no access" page
// rather than silently bouncing them to their home (which looked like a bug).
// The attempted path rides along so the page can name what was blocked.
function redirectToForbidden(pathname: string, request: NextRequest): NextResponse {
  const url = new URL("/forbidden", request.url);
  url.searchParams.set("from", pathname);
  return NextResponse.redirect(url);
}

export function middleware(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  // Always allow public marketing/auth pages through — prevents redirect loops.
  if (PUBLIC_PATHS.includes(pathname) || isAllowed(pathname, PUBLIC_PREFIXES)) {
    return NextResponse.next();
  }

  const token = request.cookies.get("access_token")?.value ?? null;
  const sessionMode = request.cookies.get("session_mode")?.value;
  const hasStaffRecovery =
    sessionMode === "staff" &&
    Boolean(request.cookies.get("staff_refresh_token")?.value);

  // A staff refresh token is enough to enter the protected shell: its first
  // API request will renew a missing/stale access token. Reject only when
  // neither an access token nor a recoverable staff session exists.
  if (!token && !hasStaffRecovery) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", pathname);
    return NextResponse.redirect(loginUrl);
  }

  const role = resolveRequestRole(
    token,
    request.cookies.get("role")?.value,
    sessionMode
  );

  // Token exists but neither it nor the fallback cookie carries a known role.
  if (!role) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // SuperAdmin is confined to /admin/* and must not enter tenant routes.
  if (role === "SuperAdmin") {
    if (!isAdminPath(pathname)) {
      return redirectToForbidden(pathname, request);
    }
    return NextResponse.next();
  }

  // Non-SuperAdmin roles must not enter /admin/* routes.
  if (isAdminPath(pathname)) {
    return redirectToForbidden(pathname, request);
  }

  // Manager can access all tenant routes.
  if (role === "Manager") {
    return NextResponse.next();
  }

  // Cashier: allowed on /orders, /payments, /kitchen, /shifts only.
  if (role === "Cashier") {
    if (isAllowed(pathname, CASHIER_ALLOWED)) {
      return NextResponse.next();
    }
    return redirectToForbidden(pathname, request);
  }

  // KitchenStaff: allowed on /kitchen, /shifts only.
  if (role === "KitchenStaff") {
    if (isAllowed(pathname, KITCHEN_STAFF_ALLOWED)) {
      return NextResponse.next();
    }
    return redirectToForbidden(pathname, request);
  }

  // Exhaustive fallback — should never be reached with the type guard above.
  return redirectTo("/login", request);
}

export const config = {
  // `hubs` is excluded alongside `api`: SignalR negotiate/WebSocket traffic to
  // /hubs/* is proxied to the backend hub, which enforces its own [Authorize].
  // Without this, page-role gating would redirect realtime requests to /login
  // (or /kitchen, /orders for Cashier/KitchenStaff) and silently break it.
  matcher: ["/((?!api|hubs|_next|favicon.ico).*)"],
};
