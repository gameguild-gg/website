import { describe, expect, it } from 'vitest';
import {
  buildPricingDailySeries,
  buildPricingInsightsDataset,
  buildPricingWindow,
  buildPricingWindowQuery,
  normalizePricingWindowDays,
  rankPromoCodesByImpact,
  rankRulesByImpact,
  summarizeDriftHealth,
  type PricingEffectivenessPayload,
  type PricingLedgerPagePayload,
} from './pricing-insights';

describe('normalizePricingWindowDays', () => {
  it('clamps to the supported window', () => {
    expect(normalizePricingWindowDays(null)).toBe(30);
    expect(normalizePricingWindowDays(0)).toBe(30);
    expect(normalizePricingWindowDays(-5)).toBe(30);
    expect(normalizePricingWindowDays(Number.NaN)).toBe(30);
    expect(normalizePricingWindowDays(7.9)).toBe(7);
    expect(normalizePricingWindowDays(10_000)).toBe(366);
  });
});

describe('buildPricingWindow / buildPricingWindowQuery', () => {
  it('derives an inclusive UTC window anchored on now', () => {
    const now = new Date('2026-10-20T12:00:00Z');
    expect(buildPricingWindow(14, now)).toEqual({
      fromUtc: '2026-10-06T12:00:00.000Z',
      toUtc: '2026-10-20T12:00:00.000Z',
    });
  });

  it('clamps the day count before deriving the window', () => {
    const now = new Date('2026-10-20T12:00:00Z');
    expect(buildPricingWindow(1, now).fromUtc).toBe('2026-10-19T12:00:00.000Z');
  });

  it('renders the shared query string with encoded timestamps', () => {
    const now = new Date('2026-10-20T12:00:00Z');
    expect(buildPricingWindowQuery(7, now)).toBe(
      'fromUtc=2026-10-13T12%3A00%3A00.000Z&toUtc=2026-10-20T12%3A00%3A00.000Z',
    );
  });
});

describe('buildPricingDailySeries', () => {
  it('buckets entries by UTC decision date and sorts oldest-first', () => {
    const series = buildPricingDailySeries([
      { decidedAtUtc: '2026-10-19T08:00:00Z', ruleAdjustmentTotal: 2, promoDiscountTotal: 3, finalAmount: 95 },
      { decidedAtUtc: '2026-10-19T22:30:00Z', ruleAdjustmentTotal: 1, promoDiscountTotal: 0, finalAmount: 49 },
      { decidedAtUtc: '2026-10-18T10:00:00Z', ruleAdjustmentTotal: 0, promoDiscountTotal: 5, finalAmount: 45 },
      null,
      { decidedAtUtc: 'not-a-date' },
    ]);
    expect(series).toEqual([
      { dateUtc: '2026-10-18', decisions: 1, discountGiven: 5, finalRevenue: 45 },
      { dateUtc: '2026-10-19', decisions: 2, discountGiven: 6, finalRevenue: 144 },
    ]);
  });

  it('tolerates missing entries', () => {
    expect(buildPricingDailySeries(null)).toEqual([]);
    expect(buildPricingDailySeries(undefined)).toEqual([]);
  });
});

describe('rankPromoCodesByImpact', () => {
  it('ranks by discount given, then redemptions, then code; caps the list', () => {
    const ranked = rankPromoCodesByImpact(
      [
        { code: 'TEN-OFF', redemptions: 4, discountGiven: 40, baselineRevenue: 400, revenueInfluenced: 360 },
        { code: 'WELCOME', redemptions: 20, discountGiven: 100, baselineRevenue: 1_000, revenueInfluenced: 900 },
        { code: 'AAA-TIE', redemptions: 9, discountGiven: 40, baselineRevenue: 500, revenueInfluenced: 460 },
        { code: 'ZZZ-TIE', redemptions: 5, discountGiven: 40, baselineRevenue: 500, revenueInfluenced: 460 },
        null,
        { code: '  ', redemptions: 1, discountGiven: 999, baselineRevenue: 0, revenueInfluenced: 0 },
      ],
      3,
    );
    expect(ranked.map((row) => row.code)).toEqual(['(unknown code)', 'WELCOME', 'AAA-TIE']);
    expect(ranked[1]?.discountRate).toBeCloseTo(0.1);
  });

  it('derives a zero rate when baseline revenue is absent', () => {
    const [row] = rankPromoCodesByImpact([{ code: 'FREE', redemptions: 1, discountGiven: 5 }]);
    expect(row?.discountRate).toBe(0);
  });
});

describe('rankRulesByImpact', () => {
  it('ranks by adjustment total and derives the net revenue delta', () => {
    const ranked = rankRulesByImpact([
      { ruleId: 'b-rule', decisions: 3, adjustmentTotal: 10, baselineRevenue: 200, finalRevenue: 190 },
      { ruleId: 'a-rule', decisions: 9, adjustmentTotal: 50, baselineRevenue: 600, finalRevenue: 550 },
      null,
    ]);
    expect(ranked.map((row) => row.ruleId)).toEqual(['a-rule', 'b-rule']);
    expect(ranked[0]?.netDelta).toBe(-50);
    expect(ranked[1]?.netDelta).toBe(-10);
  });
});

describe('summarizeDriftHealth', () => {
  it('maps known statuses, buckets unknown ones and computes the drift rate over validated entries', () => {
    const health = summarizeDriftHealth([
      { driftStatus: 'Verified', entries: 120 },
      { driftStatus: 'NotValidated', entries: 60 },
      { driftStatus: 'Drifted', entries: 5 },
      { driftStatus: 'Exotic', entries: 2 },
      null,
    ]);
    expect(health).toEqual({
      total: 187,
      verified: 120,
      notValidated: 60,
      drifted: 5,
      other: 2,
      validated: 125,
      driftRate: 5 / 125,
    });
  });

  it('returns a zero rate when nothing was validated', () => {
    const health = summarizeDriftHealth([{ driftStatus: 'NotValidated', entries: 10 }]);
    expect(health.driftRate).toBe(0);
    expect(health.total).toBe(10);
  });
});

describe('buildPricingInsightsDataset', () => {
  const effectiveness: PricingEffectivenessPayload = {
    fromUtc: '2026-09-20T00:00:00Z',
    toUtc: '2026-10-20T00:00:00Z',
    totals: {
      entryCount: 210,
      baselineRevenue: 10_000,
      finalRevenue: 9_100,
      discountGiven: 900,
      entriesWithPromo: 120,
      entriesWithRule: 40,
      promoRedemptions: 150,
    },
    topPromoCodes: [{ code: 'WELCOME', redemptions: 100, discountGiven: 500, baselineRevenue: 5_000, revenueInfluenced: 4_500 }],
    topRules: [{ ruleId: 'volume-5', decisions: 40, adjustmentTotal: 400, baselineRevenue: 4_000, finalRevenue: 3_600 }],
    stackingOutcomes: [
      { stackingRuleName: 'BestDiscountWins', decisions: 150 },
      { stackingRuleName: '', decisions: 40 },
      { stackingRuleName: 'BestDiscountWins', decisions: 20 },
    ],
    driftOutcomes: [
      { driftStatus: 'Verified', entries: 80 },
      { driftStatus: 'Drifted', entries: 4 },
      { driftStatus: 'NotValidated', entries: 126 },
    ],
  };

  const ledgerPage: PricingLedgerPagePayload = {
    items: [
      { decidedAtUtc: '2026-10-19T08:00:00Z', ruleAdjustmentTotal: 0, promoDiscountTotal: 10, finalAmount: 90 },
      { decidedAtUtc: '2026-10-18T08:00:00Z', ruleAdjustmentTotal: 5, promoDiscountTotal: 0, finalAmount: 95 },
    ],
    totalCount: 210,
    skip: 0,
    take: 2,
    pageNumber: 1,
    pageSize: 2,
    totalPages: 105,
  };

  it('derives the summary, discount rate and feed truncation flag', () => {
    const dataset = buildPricingInsightsDataset(effectiveness, ledgerPage);
    expect(dataset.summary.entryCount).toBe(210);
    expect(dataset.summary.discountRate).toBeCloseTo(0.09);
    expect(dataset.summary.feedTruncated).toBe(true);
    expect(dataset.summary.feedTotalCount).toBe(210);
    expect(dataset.summary.windowStartUtc).toBe('2026-09-20T00:00:00Z');
  });

  it('aggregates stacking outcomes by name, largest first', () => {
    const dataset = buildPricingInsightsDataset(effectiveness, ledgerPage);
    expect(dataset.stackingOutcomes).toEqual([
      { name: 'BestDiscountWins', decisions: 170 },
      { name: '(no stacking rule)', decisions: 40 },
    ]);
  });

  it('keeps ranked discounts/rules and drift health alongside the daily volume', () => {
    const dataset = buildPricingInsightsDataset(effectiveness, ledgerPage);
    expect(dataset.topPromoCodes[0]?.code).toBe('WELCOME');
    expect(dataset.topRules[0]?.ruleId).toBe('volume-5');
    expect(dataset.driftHealth.driftRate).toBeCloseTo(4 / 84);
    expect(dataset.dailySeries.map((bucket) => bucket.dateUtc)).toEqual(['2026-10-18', '2026-10-19']);
  });

  it('tolerates a fully missing payload', () => {
    const dataset = buildPricingInsightsDataset({}, null);
    expect(dataset.summary.entryCount).toBe(0);
    expect(dataset.summary.discountRate).toBe(0);
    expect(dataset.summary.feedTruncated).toBe(false);
    expect(dataset.dailySeries).toEqual([]);
    expect(dataset.topPromoCodes).toEqual([]);
    expect(dataset.driftHealth.total).toBe(0);
  });
});
