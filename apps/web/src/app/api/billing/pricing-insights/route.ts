import { getRequestAuthContext } from "@/auth";
import { buildPricingWindow, buildPricingWindowQuery, normalizePricingWindowDays } from "@/lib/billing/pricing-insights";
import { type NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

/** Ledger feed page cap for the daily-volume series; the API itself caps take at 100. */
const FEED_TAKE = 100;

function apiBaseUrl(): string {
  return (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080").replace(/\/$/, "");
}

/**
 * Browser-facing pricing-insights dashboard proxy (issue #400). The caller's session
 * token authorizes both upstream reads server-side; the billing API keeps enforcing
 * the SystemAdmin policy on the pricing-discounts-ledger controller. Returns the
 * discount-effectiveness aggregate plus one page of the ledger feed (for the daily
 * volume series) in a single payload so the console renders from one authenticated
 * fetch.
 */
export async function GET(request: NextRequest): Promise<Response> {
  const { token } = (await getRequestAuthContext()) as { token: string | null };
  if (!token) return NextResponse.json({ error: "Authentication required." }, { status: 401 });

  const days = normalizePricingWindowDays(
    Number(new URL(request.url).searchParams.get("days") ?? Number.NaN),
  );
  const query = buildPricingWindowQuery(days);
  const headers = { authorization: `Bearer ${token}`, accept: "application/json", "content-type": "application/json" };

  const effectivenessFetch = fetch(`${apiBaseUrl()}/api/v1/billing/pricing-discounts-ledger/analytics/effectiveness?${query}`, {
    headers,
    cache: "no-store",
    signal: request.signal,
  });
  const ledgerFetch = fetch(`${apiBaseUrl()}/api/v1/billing/pricing-discounts-ledger?${query}&skip=0&take=${FEED_TAKE}`, {
    headers,
    cache: "no-store",
    signal: request.signal,
  });
  const effectivenessResult = await effectivenessFetch;
  const ledgerResult = await ledgerFetch;

  if (!effectivenessResult.ok) {
    return new Response(await effectivenessResult.text() || null, {
      status: effectivenessResult.status,
      headers: { "content-type": effectivenessResult.headers.get("content-type") ?? "application/json", "cache-control": "no-store" },
    });
  }

  const effectivenessPayload: unknown = await effectivenessResult.json().catch(() => null);
  const ledgerPayload: unknown = ledgerResult.ok ? await ledgerResult.json().catch(() => null) : null;
  const window = buildPricingWindow(days);

  return NextResponse.json(
    {
      days,
      fromUtc: window.fromUtc,
      toUtc: window.toUtc,
      effectiveness: effectivenessPayload ?? {},
      ledger: ledgerPayload,
    },
    { headers: { "cache-control": "no-store" } },
  );
}
