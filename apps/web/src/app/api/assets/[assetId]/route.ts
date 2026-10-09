import { getToken } from "@/auth";
import { assertSafeServiceUrl } from "@/lib/security/safe-remote-url";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

function apiBaseUrl(): string {
  return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
}

async function proxy(upstream: Response): Promise<Response> {
  const body = await upstream.arrayBuffer();
  return new Response(upstream.status === 204 || upstream.status === 304 ? null : body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: {
      "content-type": upstream.headers.get("content-type") || "application/json",
      "cache-control": "no-store",
    },
  });
}

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ assetId: string }> },
): Promise<Response> {
  const token = await getToken();
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });
  const { assetId } = await params;
  const includeContent = request.nextUrl.searchParams.get("includeContent") !== "false";
  const upstream = await fetch(
    assertSafeServiceUrl(`${apiBaseUrl()}/v1/assets/${encodeURIComponent(assetId)}?includeContent=${includeContent}`, apiBaseUrl()),
    { headers: { authorization: `Bearer ${token}` }, cache: "no-store", signal: request.signal, redirect: "error" },
  );
  return proxy(upstream);
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ assetId: string }> },
): Promise<Response> {
  const token = await getToken();
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });
  const { assetId } = await params;
  const upstream = await fetch(assertSafeServiceUrl(`${apiBaseUrl()}/v1/assets/${encodeURIComponent(assetId)}`, apiBaseUrl()), {
    redirect: "error",
    method: "DELETE",
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
    signal: request.signal,
  });
  return proxy(upstream);
}
