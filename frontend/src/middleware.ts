// TODO: Replace cookie-based auth with Keycloak session validation.
import { NextRequest, NextResponse } from "next/server";

type Role = "Manager" | "Cashier" | "KitchenStaff";

const ROLE_VALUES: Role[] = ["Manager", "Cashier", "KitchenStaff"];

function isValidRole(value: string): value is Role {
  return (ROLE_VALUES as string[]).includes(value);
}

const CASHIER_ALLOWED = ["/orders", "/kitchen"];
const KITCHEN_STAFF_ALLOWED = ["/kitchen"];

function isAllowed(pathname: string, allowed: string[]): boolean {
  return allowed.some(
    (prefix) => pathname === prefix || pathname.startsWith(prefix + "/")
  );
}

function redirectTo(destination: string, request: NextRequest): NextResponse {
  return NextResponse.redirect(new URL(destination, request.url));
}

export function middleware(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  // Always allow the login page through — prevents redirect loops.
  if (pathname === "/login") {
    return NextResponse.next();
  }

  const token = request.cookies.get("access_token")?.value;
  const rawRole = request.cookies.get("role")?.value;

  // No token → send to login with a `from` param for post-login redirect.
  if (!token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // Token exists but role is absent or unrecognised → treat as unauthenticated.
  if (!rawRole || !isValidRole(rawRole)) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", pathname);
    return NextResponse.redirect(loginUrl);
  }

  const role: Role = rawRole;

  // Manager can access everything.
  if (role === "Manager") {
    return NextResponse.next();
  }

  // Cashier: allowed on /orders and /kitchen only.
  if (role === "Cashier") {
    if (isAllowed(pathname, CASHIER_ALLOWED)) {
      return NextResponse.next();
    }
    return redirectTo("/orders", request);
  }

  // KitchenStaff: allowed on /kitchen only.
  if (role === "KitchenStaff") {
    if (isAllowed(pathname, KITCHEN_STAFF_ALLOWED)) {
      return NextResponse.next();
    }
    return redirectTo("/kitchen", request);
  }

  // Exhaustive fallback — should never be reached with the type guard above.
  return redirectTo("/login", request);
}

export const config = {
  matcher: ["/((?!api|_next|favicon.ico).*)"],
};
