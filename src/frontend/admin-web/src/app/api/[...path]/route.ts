import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";
import {
  getRequestHeadersForLog,
  getResponseBodyForLog,
  logDevelopmentHttp,
} from "@/lib/dev-http-logging";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const METHODS_WITHOUT_BODY = new Set(["GET", "HEAD"]);
const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

type RouteContext = {
  params: Promise<{
    path: string[];
  }>;
};

function getGatewayBaseUrl() {
  const baseUrl = process.env.API_GATEWAY_INTERNAL_URL;

  if (!baseUrl) {
    return null;
  }

  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function getFrontendPublicOrigin(request: NextRequest) {
  const configuredUrl = process.env.FRONTEND_PUBLIC_URL;

  if (configuredUrl) {
    return new URL(trimTrailingSlash(configuredUrl)).origin;
  }

  return request.nextUrl.origin;
}

function getRequestSourceOrigin(request: NextRequest) {
  const origin = request.headers.get("origin");

  if (origin) {
    return new URL(origin).origin;
  }

  const referer = request.headers.get("referer");

  if (referer) {
    return new URL(referer).origin;
  }

  return null;
}

function isCsrfProtectedRequest(request: NextRequest) {
  return !SAFE_METHODS.has(request.method.toUpperCase());
}

function isSameOriginRequest(request: NextRequest) {
  if (!isCsrfProtectedRequest(request)) {
    return true;
  }

  try {
    return getRequestSourceOrigin(request) === getFrontendPublicOrigin(request);
  } catch {
    return false;
  }
}

function getJwtSubject(accessToken: string | undefined) {
  if (!accessToken) {
    return undefined;
  }

  try {
    const [, payload] = accessToken.split(".");
    if (!payload) {
      return undefined;
    }

    const normalizedPayload = payload.replace(/-/g, "+").replace(/_/g, "/");
    const paddedPayload = normalizedPayload.padEnd(
      Math.ceil(normalizedPayload.length / 4) * 4,
      "="
    );
    const claims = JSON.parse(Buffer.from(paddedPayload, "base64").toString("utf8")) as {
      sub?: unknown;
    };

    return typeof claims.sub === "string" && claims.sub.length > 0
      ? claims.sub
      : undefined;
  } catch {
    return undefined;
  }
}

function buildProxyHeaders(request: NextRequest, accessToken?: string) {
  const headers = new Headers(request.headers);

  headers.delete("host");
  headers.delete("content-length");
  headers.delete("connection");
  headers.delete("cookie");
  headers.delete("client-id");

  if (accessToken) {
    headers.set("authorization", `Bearer ${accessToken}`);
  }

  const clientId = getJwtSubject(accessToken);
  if (clientId) {
    headers.set("client-id", clientId);
  }

  return headers;
}

async function proxyRequest(request: NextRequest, { params }: RouteContext) {
  const gatewayBaseUrl = getGatewayBaseUrl();

  if (!gatewayBaseUrl) {
    return NextResponse.json(
      { message: "API_GATEWAY_INTERNAL_URL is not configured." },
      { status: 500 }
    );
  }

  if (!isSameOriginRequest(request)) {
    return NextResponse.json(
      { message: "Cross-site request rejected." },
      { status: 403 }
    );
  }

  const { path } = await params;
  const upstreamUrl = new URL(
    `${gatewayBaseUrl}/gateway/${path.join("/")}${request.nextUrl.search}`
  );
  const session = await auth();

  if (session?.error) {
    return NextResponse.json(
      { message: "Authentication session expired. Please sign in again." },
      { status: 401 }
    );
  }

  const requestBody = METHODS_WITHOUT_BODY.has(request.method)
    ? undefined
    : await request.arrayBuffer();
  const proxyHeaders = buildProxyHeaders(request, session?.accessToken);
  const clientId = proxyHeaders.get("client-id") ?? undefined;

  logDevelopmentHttp("backend api request", {
    method: request.method,
    url: upstreamUrl.toString(),
    accessToken: session?.accessToken,
    clientId,
    headers: getRequestHeadersForLog(proxyHeaders),
    body: requestBody ? new TextDecoder().decode(requestBody) : undefined,
  });

  const upstreamResponse = await fetch(upstreamUrl, {
    method: request.method,
    headers: proxyHeaders,
    body: requestBody,
    cache: "no-store",
    redirect: "manual",
  });

  logDevelopmentHttp("backend api response", {
    method: request.method,
    url: upstreamUrl.toString(),
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers: getRequestHeadersForLog(upstreamResponse.headers),
    body: await getResponseBodyForLog(upstreamResponse),
  });

  return new Response(upstreamResponse.body, {
    status: upstreamResponse.status,
    headers: upstreamResponse.headers,
  });
}

export async function GET(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function POST(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function PUT(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function OPTIONS(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function HEAD(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}
