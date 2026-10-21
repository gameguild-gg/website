import { PricingInsightsConsole } from '@/components/billing/pricing-insights-console';
import { buildPricingInsightsDataset, buildPricingWindowQuery, normalizePricingWindowDays, type PricingEffectivenessPayload, type PricingLedgerPagePayload } from '@/lib/billing/pricing-insights';
import { redirect } from '@/i18n/navigation';
import { getRequestAuthContext, auth } from '@/auth';

/** Ledger feed page cap for the daily-volume series; the API itself caps take at 100. */
const FEED_TAKE = 100;

/** Server-side data loader for the pricing-insights dashboard (issue #400). The
 * caller's session token authorizes both reads; the SystemAdmin policy on the
 * pricing-discounts-ledger controller stays enforced on the API. The console
 * refetches on window change through the scoped proxy. */
async function loadPricingInsights(days: number, token: string): Promise<{
  effectiveness: PricingEffectivenessPayload;
  ledger: PricingLedgerPagePayload | null;
} | null> {
  const baseUrl = (process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080').replace(/\/$/, '');
  const headers = { authorization: `Bearer ${token}`, accept: 'application/json', 'content-type': 'application/json' };
  const query = buildPricingWindowQuery(days);
  const effectivenessFetch = fetch(`${baseUrl}/api/v1/billing/pricing-discounts-ledger/analytics/effectiveness?${query}`, {
    headers,
    cache: 'no-store',
  });
  const ledgerFetch = fetch(`${baseUrl}/api/v1/billing/pricing-discounts-ledger?${query}&skip=0&take=${FEED_TAKE}`, {
    headers,
    cache: 'no-store',
  });
  const effectivenessResult = await effectivenessFetch;
  if (!effectivenessResult.ok) return null;
  const effectivenessPayload: unknown = await effectivenessResult.json().catch(() => null);
  const ledgerResult = await ledgerFetch;
  const ledgerPayload: unknown = ledgerResult.ok ? await ledgerResult.json().catch(() => null) : null;
  return {
    effectiveness: effectivenessPayload ?? {},
    ledger: ledgerPayload ?? null,
  };
}

/**
 * Pricing and discount-effectiveness dashboard (issue #400): the economy console
 * surface over the durable pricing-decision ledger — decision volume over time,
 * discount effectiveness, pricing-rule revenue impact and drift/validation status.
 * Session authentication is enforced here.
 */
export default async function PricingInsightsPage({
  params,
}: { params: Promise<{ locale: string }> }): Promise<React.JSX.Element> {
  const { locale } = await params;
  const session = await auth();
  if (!session || typeof session === 'function') {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/console/economy/pricing-insights' } }, locale });
    throw new Error('Unauthenticated console access');
  }
  const { token } = (await getRequestAuthContext()) as { token: string | null };
  if (!token) {
    redirect({ href: { pathname: '/sign-in', query: { callbackUrl: '/console/economy/pricing-insights' } }, locale });
    throw new Error('Unauthenticated console access');
  }

  const days = normalizePricingWindowDays(30);
  const initial = await loadPricingInsights(days, token);
  const dataset = initial ? buildPricingInsightsDataset(initial.effectiveness, initial.ledger) : null;

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 sm:p-6">
      <header>
        <h1 className="text-3xl font-bold tracking-tight">Pricing insights</h1>
        <p className="text-muted-foreground">
          Financial reporting over the pricing-decision ledger: how many prices were calculated, which discounts
          actually moved revenue, what the pricing rules cost, and whether stored finals still follow from their
          own calculation breakdown.
        </p>
      </header>
      {dataset === null ? (
        <p className="text-sm text-muted-foreground">
          The pricing analytics API rejected the initial dashboard query. Try a different window or retry shortly.
        </p>
      ) : (
        <PricingInsightsConsole initialDays={days} initialDataset={dataset} />
      )}
    </div>
  );
}
