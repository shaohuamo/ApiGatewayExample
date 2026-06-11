import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";

const PROTECTED_PATHS = ["/products"];

export const proxy = auth((request) => {
  const isProtectedPath = PROTECTED_PATHS.some((path) =>
    request.nextUrl.pathname.startsWith(path)
  );

  if (!isProtectedPath || request.auth) {
    return NextResponse.next();
  }

  const loginUrl = new URL("/login", request.nextUrl.origin);
  loginUrl.searchParams.set("callbackUrl", `${request.nextUrl.pathname}${request.nextUrl.search}`);

  return NextResponse.redirect(loginUrl);
});

export const config = {
  matcher: ["/products/:path*"],
};
