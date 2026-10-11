/**
 * @game-guild/client - CommerceBillingPricingDiscountsLedger Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceBillingPricingDiscountsLedgerModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List pricing-decision ledger entries
   *
   * Pages through the durable pricing-decision ledger: one row per checkout/renewal calculation that produced a final price, with headline amounts, applied promo codes, winning pricing rule and drift status. Filterable by tenant, order, subscription, promo code, decision type, drift status and decision date range.
   */
  async getBillingPricingDiscountsLedgerForGetBillingPricingDiscountsLedger(query?: {
    tenantId?: string;
    orderId?: string;
    subscriptionId?: string;
    promoCode?: string;
    decisionType?: string;
    driftStatus?: string;
    fromUtc?: string;
    toUtc?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultPricingDecisionLedgerEntryDto, ApiError>> {
    const url = '/api/v1/billing/pricing-discounts-ledger';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultPricingDecisionLedgerEntryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Validate a sample of ledger entries for pricing drift
   *
   * Drift detection over the pricing-decision ledger: samples entries (preferring never-validated ones), recomputes each final amount from its own breakdown document and stamps the entry Verified or Drifted. Drifted entries — where the stored final no longer follows from the recorded arithmetic — are surfaced first for review.
   */
  async postBillingPricingDiscountsLedgerValidateDrift(
    body: Types.CommerceBillingValidatePricingLedgerDriftInput,
  ): Promise<Result<Types.CommerceBillingPricingLedgerDriftValidationResult, ApiError>> {
    const url = '/api/v1/billing/pricing-discounts-ledger/:validate-drift';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceBillingValidatePricingLedgerDriftInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingPricingLedgerDriftValidationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a pricing-decision ledger entry by id
   *
   * Retrieves one pricing-decision ledger entry, including the full calculation breakdown JSON (base, per-rule adjustments, per-code promo applications, caps, final amount), correlation ids and the drift-validation outcome.
   */
  async getBillingPricingDiscountsLedgerForGetBillingPricingDiscountsLedgerByEntryId(
    entryId: string,
  ): Promise<Result<Types.CommerceBillingPricingDecisionLedgerEntryDetailDto, ApiError>> {
    const url = `/api/v1/billing/pricing-discounts-ledger/${entryId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingPricingDecisionLedgerEntryDetailDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Discount effectiveness and pricing analytics over the ledger
   *
   * Aggregates the pricing-decision ledger over a window: total decisions, baseline vs final revenue, discount given, promo redemptions, per-code effectiveness (redemptions, discount given, baseline vs influenced revenue), per-pricing-rule impact, stacking outcomes and drift status counts. Feeds the monetization reporting dashboard.
   */
  async getBillingPricingDiscountsLedgerAnalyticsEffectiveness(query?: {
    fromUtc?: string;
    toUtc?: string;
    tenantId?: string;
  }): Promise<Result<Types.CommerceBillingPricingLedgerEffectivenessAggregate, ApiError>> {
    const url = '/api/v1/billing/pricing-discounts-ledger/analytics/effectiveness';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingPricingLedgerEffectivenessAggregateSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceBillingPricingDiscountsLedgerModule(client: ApiClient): CommerceBillingPricingDiscountsLedgerModule {
  return new CommerceBillingPricingDiscountsLedgerModule(client);
}
