import { getRequestAuthContext } from "@/auth";
import { buildPermissionChangeQueryText, normalizePermissionWindowDays } from "@/lib/audit/permission-patterns";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

function apiBaseUrl(): string {
  return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
}

/**
 * Browser-facing permission-pattern dashboard proxy (issue #297). The caller's session
 * token authorizes both upstream reads server-side; the API keeps enforcing the audit
 * query authorization and data masking. Returns the DSL execution result (aggregates +
 * masked rows) and the tenant's open/acknowledged security alerts in one payload so the
 * console renders from a single authenticated fetch.
 */
export async function GET(request: NextRequest): Promise<Response> {
  const { token } = (await getRequestAuthContext()) as { token: string | null };
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });

  const days = normalizePermissionWindowDays(
    Number(new URL(request.url).searchParams.get("days") ?? Number.NaN),
  );
  const queryText = buildPermissionChangeQueryText(days);
  const headers = { authorization: `Bearer ${token}`, accept: "application/json", "content-type": "application/json" };

  const queryFetch = fetch(`${apiBaseUrl()}/v1/audit-queries/:execute`, {
    method: "POST",
    headers,
    cache: "no-store",
    signal: request.signal,
    body: JSON.stringify({ queryText, skip: 0, take: 200 }),
  });
  const alertsFetch = fetch(`${apiBaseUrl()}/api/audit/security-events/alerts?take=50`, {
    headers,
    cache: "no-store",
    signal: request.signal,
  });
  const queryResult = await queryFetch;
  const alertsResult = await alertsFetch;

  if (!queryResult.ok) {
    return new Response(await queryResult.text() || null, {
      status: queryResult.status,
      headers: { "content-type": queryResult.headers.get("content-type") ?? "application/json", "cache-control": "no-store" },
    });
  }

  const queryPayload: unknown = await queryResult.json().catch(() => null);
  const alertsPayload: unknown = alertsResult.ok ? await alertsResult.json().catch(() => null) : null;

  return NextResponse.json(
    {
      days,
      queryText,
      query: queryPayload,
      alerts: Array.isArray(alertsPayload) ? alertsPayload : [],
    },
    { headers: { "cache-control": "no-store" } },
  );
}
