import { NextRequest, NextResponse } from "next/server";

type Role = "Manager" | "Cashier" | "KitchenStaff" | "SuperAdmin";

const ROLE_VALUES: Role[] = ["Manager", "Cashier", "KitchenStaff", "SuperAdmin"];

function isValidRole(value: string): value is Role {
  return (ROLE_VALUES as string[]).includes(value);
}

const CASHIER_ALLOWED = ["/orders", "/payments", "/kitchen", "/shifts"];
const KITCHEN_STAFF_ALLOWED = ["/kitchen", "/shifts"];
const PUBLIC_PATHS = [
  "/",
  "/login",
  "/auth/callback",
  "/signup",
  "/signup/success",
  "/signup/cancelled",
  "/set-password",
];

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

export function middleware(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  // Always allow public marketing/auth pages through — prevents redirect loops.
  if (PUBLIC_PATHS.includes(pathname)) {
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

  // SuperAdmin is confined to /admin/* and must not enter tenant routes.
  if (role === "SuperAdmin") {
    if (!isAdminPath(pathname)) {
      return redirectTo("/admin/dashboard", request);
    }
    return NextResponse.next();
  }

  // Non-SuperAdmin roles must not enter /admin/* routes.
  if (isAdminPath(pathname)) {
    return redirectTo("/dashboard", request);
  }

  // Manager can access all tenant routes.
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
