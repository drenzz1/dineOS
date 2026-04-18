// TODO: Replace cookie-based access_token check with Keycloak session validation.
import { NextRequest, NextResponse } from "next/server";

export function middleware(request: NextRequest): NextResponse {
  const token = request.cookies.get("access_token")?.value;

  if (!token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", request.nextUrl.pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/dashboard/:path*",
    "/kitchen/:path*",
    "/menu/:path*",
    "/reports/:path*",
    "/orders/:path*",
    "/shifts/:path*",
    "/staff/:path*",
    "/admin/:path*",
  ],
};
