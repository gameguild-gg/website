/**
 * @game-guild/client - CommercePricing Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePricingModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Run a full pricing calculation through the unified engine: base plan or custom
   * amount, add-ons, usage tiers, promo discounts, proration, tax, credits, currency —
   * itemized, explained, self-validated, and recorded in the pricing-decision ledger.
   *
   * Calculates a full price (base, add-ons, usage tiers, discounts, proration, tax, credits) through the single centralized pricing engine and records the decision for audit.
   */
  async postPricingCalculate(body: Types.CommercePaymentsPricingCalculationInput): Promise<Result<Types.CommercePaymentsPricingCalculationResult, ApiError>> {
    const url = '/api/v1/pricing/:calculate';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPricingCalculationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPricingCalculationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Preview a plan change before finalizing: the immediate prorated charge and the
   * next-renewal amount side by side. Read-only — no ledger decision, no credit spend.
   *
   * Previews what a plan change costs before it is applied: the immediate prorated charge and the next renewal amount, with discounts, tax, and an itemized breakdown.
   */
  async postPricingPreview(body: Types.CommercePaymentsPricingPreviewInput): Promise<Result<Types.CommercePaymentsPricingPreviewResult, ApiError>> {
    const url = '/api/v1/pricing/:preview';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPricingPreviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPricingPreviewResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a stored price calculation by id
   *
   * Retrieves one recorded pricing calculation from the pricing-decision ledger, including the full itemized breakdown (base, add-ons, usage, discounts, proration, tax, credits) that produced the final amount.
   */
  async getPricingCalculations(calculationId: string): Promise<Result<Types.CommerceBillingPricingDecisionLedgerEntryDetailDto, ApiError>> {
    const url = `/api/v1/pricing/calculations/${calculationId}`;

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
   * Validate the accuracy of a stored price calculation
   *
   * Re-validates one stored pricing calculation: recomputes the final amount from the recorded breakdown and checks the calculation invariants — totals match line items, discounts do not exceed the subtotal, tax rates are plausible, proration stays within the plan price, and no rounding drift accumulated.
   */
  async postPricingValidate(calculationId: string): Promise<Result<Types.CommerceBillingPricingCalculationValidationResult, ApiError>> {
    const url = `/api/v1/pricing/validate/${calculationId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceBillingPricingCalculationValidationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePricingModule(client: ApiClient): CommercePricingModule {
  return new CommercePricingModule(client);
}
