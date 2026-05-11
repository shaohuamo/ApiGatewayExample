import { NextRequest, NextResponse } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const METHODS_WITHOUT_BODY = new Set(["GET", "HEAD"]);

type RouteContext = {
  params: Promise<{
    path: string[];
  }>;
};

function getCollectorBaseUrl() {
  const baseUrl = process.env.OTEL_COLLECTOR_INTERNAL_URL;

  if (!baseUrl) {
    return null;
  }

  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}

function buildProxyHeaders(request: NextRequest) {
  const headers = new Headers(request.headers);

  headers.delete("host");
  headers.delete("content-length");
  headers.delete("connection");

  return headers;
}

async function proxyRequest(request: NextRequest, { params }: RouteContext) {
  const collectorBaseUrl = getCollectorBaseUrl();

  if (!collectorBaseUrl) {
    return NextResponse.json(
      { message: "OTEL_COLLECTOR_INTERNAL_URL is not configured." },
      { status: 500 }
    );
  }

  const { path } = await params;
  const upstreamUrl = new URL(
    `${collectorBaseUrl}/${path.join("/")}${request.nextUrl.search}`
  );

  const upstreamResponse = await fetch(upstreamUrl, {
    method: request.method,
    headers: buildProxyHeaders(request),
    body: METHODS_WITHOUT_BODY.has(request.method)
      ? undefined
      : await request.arrayBuffer(),
    cache: "no-store",
    redirect: "manual",
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