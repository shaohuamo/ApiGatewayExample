import { NextResponse } from "next/server";
import {
  CULTURE_COOKIE_NAME,
  formatCultureCookie,
  getCultureCookieDomain,
  isSupportedLocale,
} from "@/lib/i18n/config";

function getSafeReturnUrl(value: string | null) {
  if (!value || !value.startsWith("/") || value.startsWith("//")) {
    return "/";
  }

  return value;
}

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function getPublicBaseUrl(request: Request, requestUrl: URL) {
  const configuredUrl = process.env.FRONTEND_PUBLIC_URL;
  if (configuredUrl) {
    return trimTrailingSlash(configuredUrl);
  }

  const forwardedHost = request.headers.get("x-forwarded-host");
  const forwardedProto = request.headers.get("x-forwarded-proto") ?? requestUrl.protocol.replace(":", "");

  if (forwardedHost) {
    return `${forwardedProto}://${forwardedHost}`;
  }

  return requestUrl.origin;
}

export function GET(request: Request) {
  const url = new URL(request.url);
  const culture = url.searchParams.get("culture");
  const returnUrl = getSafeReturnUrl(url.searchParams.get("returnUrl"));
  const publicBaseUrl = getPublicBaseUrl(request, url);

  if (!isSupportedLocale(culture)) {
    return NextResponse.redirect(new URL(returnUrl, publicBaseUrl));
  }

  const response = NextResponse.redirect(new URL(returnUrl, publicBaseUrl));
  const cookieDomain = getCultureCookieDomain(url.hostname);
  response.cookies.set(CULTURE_COOKIE_NAME, formatCultureCookie(culture), {
    path: "/",
    domain: cookieDomain,
    httpOnly: false,
    sameSite: "lax",
    secure: url.protocol === "https:" || Boolean(cookieDomain),
    maxAge: 60 * 60 * 24 * 365,
  });

  return response;
}
