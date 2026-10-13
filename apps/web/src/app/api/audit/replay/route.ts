import { getRequestAuthContext } from "@/auth";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

function apiBaseUrl(): string {
  return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
}

/**
 * Browser-facing audit replay capture proxy (issue #169). The caller's session token
 * authorizes the capture server-side; the API enforces the system-administrator guard,
 * durable access audit, bounded selection and the integrity digest. The original snapshot
 * bytes and digest header pass through unmodified so the client replay core can verify
 * them before building any timeline.
 */
export async function GET(request: NextRequest): Promise<Response> {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });
  const start = request.nextUrl.searchParams.get("start")?.trim();
  const end = request.nextUrl.searchParams.get("end")?.trim();
  if (!start || !end) {
    return NextResponse.json({ error: "start and end are required ISO-8601 UTC timestamps." }, { status: 400 });
  }

  const upstreamUrl = new URL(`${apiBaseUrl()}/v1/admin/audit-logs/replay/snapshots`);
  upstreamUrl.searchParams.set("start", start);
  upstreamUrl.searchParams.set("end", end);
  const upstream = await fetch(upstreamUrl.toString(), {
    headers: { authorization: `Bearer ${token}`, accept: "application/json" },
    cache: "no-store",
    signal: request.signal,
  });
  const sha256 = upstream.headers.get("X-Audit-Snapshot-SHA256");
  const json = await upstream.text();
  if (!upstream.ok) {
    return new Response(json || null, {
      status: upstream.status,
      headers: { "content-type": upstream.headers.get("content-type") || "application/json", "cache-control": "no-store" },
    });
  }
  if (!sha256) {
    return NextResponse.json({ error: "Capture returned no integrity digest." }, { status: 502 });
  }
  // The session's tenant is the authorized scope the client core verifies the snapshot against.
  return NextResponse.json({ json, sha256, tenantId }, { headers: { "cache-control": "no-store" } });
}
