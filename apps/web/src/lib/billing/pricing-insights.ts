/**
 * Pricing and discount-effectiveness analytics for the economy console (issue #400).
 * Pure transformations over the pricing-decision ledger effectiveness aggregate and
 * the paginated ledger feed — no transport here; the route proxy and the console
 * component own IO. Authorization stays server-side on the billing API
 * (SystemAdmin policy on the pricing-discounts-ledger controller).
 */

/** Overall totals block of the effectiveness aggregate. */
export interface PricingEffectivenessTotalsPayload {
  entryCount?: number;
  baselineRevenue?: number;
  finalRevenue?: number;
  discountGiven?: number;
  entriesWithPromo?: number;
  entriesWithRule?: number;
  promoRedemptions?: number;
}

/** Per-promo-code effectiveness row (API ranks by discount given, top 20). */
export interface PromoCodeEffectivenessPayload {
  code?: string;
  redemptions?: number;
  discountGiven?: number;
  baselineRevenue?: number;
  revenueInfluenced?: number;
}

/** Per-pricing-rule impact row (API ranks by adjustment total, top 20). */
export interface PricingRuleImpactPayload {
  ruleId?: string;
  decisions?: number;
  adjustmentTotal?: number;
  baselineRevenue?: number;
  finalRevenue?: number;
}

/** Stacking outcome count row. */
export interface StackingOutcomePayload {
  stackingRuleName?: string;
  decisions?: number;
}

/** Drift outcome count row. */
export interface DriftOutcomePayload {
  driftStatus?: string;
  entries?: number;
}

/** Raw shape of GET api/v1/billing/pricing-discounts-ledger/analytics/effectiveness. */
export interface PricingEffectivenessPayload {
  fromUtc?: string | null;
  toUtc?: string | null;
  tenantId?: string | null;
  totals?: PricingEffectivenessTotalsPayload | null;
  topPromoCodes?: readonly PromoCodeEffectivenessPayload[] | null;
  topRules?: readonly PricingRuleImpactPayload[] | null;
  stackingOutcomes?: readonly StackingOutcomePayload[] | null;
  driftOutcomes?: readonly DriftOutcomePayload[] | null;
}

/** Ledger feed row (headline amounts; the breakdown lives on the detail read). */
export interface PricingLedgerEntryPayload {
  id?: string;
  decisionType?: string;
  currency?: string;
  baseAmount?: number;
  ruleAdjustmentTotal?: number;
  promoDiscountTotal?: number;
  finalAmount?: number;
  appliedPromoCodes?: readonly string[] | null;
  appliedPricingRuleId?: string | null;
  appliedStackingRuleName?: string | null;
  decidedAtUtc?: string;
  driftStatus?: string;
}

/** Paged envelope of GET api/v1/billing/pricing-discounts-ledger (PagedResult<T>). */
export interface PricingLedgerPagePayload {
  items?: readonly PricingLedgerEntryPayload[] | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PricingInsightsSummary {
  readonly entryCount: number;
  readonly baselineRevenue: number;
  readonly finalRevenue: number;
  readonly discountGiven: number;
  readonly discountRate: number;
  readonly entriesWithPromo: number;
  readonly entriesWithRule: number;
  readonly promoRedemptions: number;
  readonly windowStartUtc: string | null;
  readonly windowEndUtc: string | null;
  readonly feedTruncated: boolean;
  readonly feedTotalCount: number;
}

export interface PricingDailyBucket {
  readonly dateUtc: string;
  readonly decisions: number;
  readonly discountGiven: number;
  readonly finalRevenue: number;
}

export interface PromoCodeInsight {
  readonly code: string;
  readonly redemptions: number;
  readonly discountGiven: number;
  readonly baselineRevenue: number;
  readonly revenueInfluenced: number;
  readonly discountRate: number;
}

export interface RuleImpactInsight {
  readonly ruleId: string;
  readonly decisions: number;
  readonly adjustmentTotal: number;
  readonly baselineRevenue: number;
  readonly finalRevenue: number;
  readonly netDelta: number;
}

export interface StackingOutcomeInsight {
  readonly name: string;
  readonly decisions: number;
}

export interface DriftHealth {
  readonly total: number;
  readonly verified: number;
  readonly notValidated: number;
  readonly drifted: number;
  readonly other: number;
  readonly validated: number;
  readonly driftRate: number;
}

export interface PricingInsightsDataset {
  readonly summary: PricingInsightsSummary;
  readonly dailySeries: readonly PricingDailyBucket[];
  readonly topPromoCodes: readonly PromoCodeInsight[];
  readonly topRules: readonly RuleImpactInsight[];
  readonly stackingOutcomes: readonly StackingOutcomeInsight[];
  readonly driftHealth: DriftHealth;
}

const MAX_DAYS = 366;
const MIN_DAYS = 1;
const DEFAULT_DAYS = 30;
export const TOP_INSIGHT_LIMIT = 8;

export function normalizePricingWindowDays(days: number | null | undefined): number {
  if (!Number.isFinite(days) || days === null || days === undefined || days <= 0) return DEFAULT_DAYS;
  return Math.min(MAX_DAYS, Math.max(MIN_DAYS, Math.trunc(days)));
}

/** Inclusive analytics window [fromUtc, toUtc] for the requested day count. */
export function buildPricingWindow(
  days: number,
  now: Date = new Date(),
): { fromUtc: string; toUtc: string } {
  const windowDays = normalizePricingWindowDays(days);
  const to = now.getTime();
  return { fromUtc: new Date(to - windowDays * 24 * 60 * 60 * 1000).toISOString(), toUtc: new Date(to).toISOString() };
}

/** Query string shared by the effectiveness aggregate and the ledger feed fetches. */
export function buildPricingWindowQuery(days: number, now: Date = new Date()): string {
  const { fromUtc, toUtc } = buildPricingWindow(days, now);
  return `fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}`;
}

function safeNumber(value: unknown): number {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function safeRate(numerator: number, denominator: number): number {
  return denominator > 0 ? numerator / denominator : 0;
}

/** Promo codes ranked by discount impact (discount given, then redemptions, then code). */
export function rankPromoCodesByImpact(
  codes: readonly (PromoCodeEffectivenessPayload | null | undefined)[] | null | undefined,
  limit: number = TOP_INSIGHT_LIMIT,
): PromoCodeInsight[] {
  return (codes ?? [])
    .filter((row): row is PromoCodeEffectivenessPayload => Boolean(row))
    .map((row) => {
      const discountGiven = safeNumber(row.discountGiven);
      const baselineRevenue = safeNumber(row.baselineRevenue);
      return {
        code: typeof row.code === 'string' && row.code.trim().length > 0 ? row.code : '(unknown code)',
        redemptions: safeNumber(row.redemptions),
        discountGiven,
        baselineRevenue,
        revenueInfluenced: safeNumber(row.revenueInfluenced),
        discountRate: safeRate(discountGiven, baselineRevenue),
      };
    })
    .sort(
      (a, b) =>
        b.discountGiven - a.discountGiven ||
        b.redemptions - a.redemptions ||
        a.code.localeCompare(b.code),
    )
    .slice(0, Math.max(0, limit));
}

/** Pricing rules ranked by adjustment impact (adjustment total, then decisions, then rule id). */
export function rankRulesByImpact(
  rules: readonly (PricingRuleImpactPayload | null | undefined)[] | null | undefined,
  limit: number = TOP_INSIGHT_LIMIT,
): RuleImpactInsight[] {
  return (rules ?? [])
    .filter((row): row is PricingRuleImpactPayload => Boolean(row))
    .map((row) => {
      const baselineRevenue = safeNumber(row.baselineRevenue);
      const finalRevenue = safeNumber(row.finalRevenue);
      return {
        ruleId: typeof row.ruleId === 'string' && row.ruleId.trim().length > 0 ? row.ruleId : '(unknown rule)',
        decisions: safeNumber(row.decisions),
        adjustmentTotal: safeNumber(row.adjustmentTotal),
        baselineRevenue,
        finalRevenue,
        netDelta: finalRevenue - baselineRevenue,
      };
    })
    .sort(
      (a, b) =>
        b.adjustmentTotal - a.adjustmentTotal ||
        b.decisions - a.decisions ||
        a.ruleId.localeCompare(b.ruleId),
    )
    .slice(0, Math.max(0, limit));
}

/** Buckets the ledger feed by UTC decision date; the API pages newest-first. */
export function buildPricingDailySeries(
  entries: readonly (PricingLedgerEntryPayload | null | undefined)[] | null | undefined,
): PricingDailyBucket[] {
  const buckets = new Map<string, PricingDailyBucket>();
  for (const entry of entries ?? []) {
    if (!entry) continue;
    const decidedAt = typeof entry.decidedAtUtc === 'string' ? Date.parse(entry.decidedAtUtc) : Number.NaN;
    if (!Number.isFinite(decidedAt)) continue;
    const dateUtc = new Date(decidedAt).toISOString().slice(0, 10);
    const discountGiven = safeNumber(entry.ruleAdjustmentTotal) + safeNumber(entry.promoDiscountTotal);
    const current = buckets.get(dateUtc) ?? { dateUtc, decisions: 0, discountGiven: 0, finalRevenue: 0 };
    buckets.set(dateUtc, {
      dateUtc,
      decisions: current.decisions + 1,
      discountGiven: current.discountGiven + discountGiven,
      finalRevenue: current.finalRevenue + safeNumber(entry.finalAmount),
    });
  }
  return [...buckets.values()].sort((a, b) => a.dateUtc.localeCompare(b.dateUtc));
}

/** Drift/validation health from the aggregate's status counts. */
export function summarizeDriftHealth(
  outcomes: readonly (DriftOutcomePayload | null | undefined)[] | null | undefined,
): DriftHealth {
  let verified = 0;
  let notValidated = 0;
  let drifted = 0;
  let other = 0;
  for (const outcome of outcomes ?? []) {
    if (!outcome) continue;
    const entries = safeNumber(outcome.entries);
    switch (outcome.driftStatus) {
      case 'Verified': verified += entries; break;
      case 'NotValidated': notValidated += entries; break;
      case 'Drifted': drifted += entries; break;
      default: other += entries; break;
    }
  }
  const validated = verified + drifted;
  return {
    total: verified + notValidated + drifted + other,
    verified,
    notValidated,
    drifted,
    other,
    validated,
    driftRate: safeRate(drifted, validated),
  };
}

/** Builds the dashboard dataset: summary + daily volume + ranked discounts/rules + drift health. */
export function buildPricingInsightsDataset(
  effectiveness: PricingEffectivenessPayload,
  ledgerPage: PricingLedgerPagePayload | null | undefined,
): PricingInsightsDataset {
  const totals = effectiveness.totals ?? null;
  const baselineRevenue = safeNumber(totals?.baselineRevenue);
  const discountGiven = safeNumber(totals?.discountGiven);
  const items = ledgerPage?.items ?? [];
  const feedTotalCount = safeNumber(ledgerPage?.totalCount);

  const summary: PricingInsightsSummary = {
    entryCount: safeNumber(totals?.entryCount),
    baselineRevenue,
    finalRevenue: safeNumber(totals?.finalRevenue),
    discountGiven,
    discountRate: safeRate(discountGiven, baselineRevenue),
    entriesWithPromo: safeNumber(totals?.entriesWithPromo),
    entriesWithRule: safeNumber(totals?.entriesWithRule),
    promoRedemptions: safeNumber(totals?.promoRedemptions),
    windowStartUtc: effectiveness.fromUtc ?? null,
    windowEndUtc: effectiveness.toUtc ?? null,
    feedTruncated: feedTotalCount > items.length,
    feedTotalCount,
  };

  const stackingCounts = new Map<string, number>();
  for (const row of effectiveness.stackingOutcomes ?? []) {
    if (!row) continue;
    const name =
      typeof row.stackingRuleName === 'string' && row.stackingRuleName.trim().length > 0
        ? row.stackingRuleName
        : '(no stacking rule)';
    stackingCounts.set(name, (stackingCounts.get(name) ?? 0) + safeNumber(row.decisions));
  }
  const stackingOutcomes = [...stackingCounts.entries()]
    .map(([name, decisions]) => ({ name, decisions }))
    .sort((a, b) => b.decisions - a.decisions || a.name.localeCompare(b.name));

  return {
    summary,
    dailySeries: buildPricingDailySeries(items),
    topPromoCodes: rankPromoCodesByImpact(effectiveness.topPromoCodes),
    topRules: rankRulesByImpact(effectiveness.topRules),
    stackingOutcomes,
    driftHealth: summarizeDriftHealth(effectiveness.driftOutcomes),
  };
}
