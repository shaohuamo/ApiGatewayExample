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

function appendCultureCookie(
  response: NextResponse,
  value: string,
  options: {
    domain?: string;
    maxAge: number;
    secure: boolean;
    expires?: Date;
  }
) {
  const cookieParts = [
    `${CULTURE_COOKIE_NAME}=${encodeURIComponent(value)}`,
    "Path=/",
    `Max-Age=${options.maxAge}`,
    "SameSite=Lax",
  ];

  if (options.expires) {
    cookieParts.push(`Expires=${options.expires.toUTCString()}`);
  }

  if (options.domain) {
    cookieParts.push(`Domain=${options.domain}`);
  }

  if (options.secure) {
    cookieParts.push("Secure");
  }

  response.headers.append("Set-Cookie", cookieParts.join("; "));
}

function deleteCultureCookie(response: NextResponse, secure: boolean, domain?: string) {
  appendCultureCookie(response, "", {
    domain,
    maxAge: 0,
    secure,
    expires: new Date(0),
  });
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
  const publicUrl = new URL(publicBaseUrl);
  const cookieDomain = getCultureCookieDomain(publicUrl.hostname);
  const secure = publicUrl.protocol === "https:" || Boolean(cookieDomain);

  deleteCultureCookie(response, secure);
  if (cookieDomain) {
    deleteCultureCookie(response, secure, cookieDomain);
  }

  appendCultureCookie(response, formatCultureCookie(culture), {
    domain: cookieDomain,
    maxAge: 60 * 60 * 24 * 365,
    secure,
  });

  return response;
}
