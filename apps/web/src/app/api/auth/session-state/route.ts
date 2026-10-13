import { getToken } from "@/auth";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

function apiBaseUrl(): string {
  return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
}

/**
 * Browser-facing session-state proxy (issue #221). The caller's session token
 * authenticates the upstream request; the API answers with the caller's own session
 * validity, expiry, token version, active-session count and MFA enrollment state.
 */
export async function GET(request: NextRequest): Promise<Response> {
  const token = await getToken();
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });
  const upstream = await fetch(`${apiBaseUrl()}/v1/auth/session-state`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
    signal: request.signal,
  });
  const body = await upstream.text();
  return new Response(upstream.status === 204 || upstream.status === 304 ? null : body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: {
      "content-type": upstream.headers.get("content-type") || "application/json",
      "cache-control": "no-store",
    },
  });
}
