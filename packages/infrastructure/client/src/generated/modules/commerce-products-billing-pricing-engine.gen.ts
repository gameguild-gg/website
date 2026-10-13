/**
 * @game-guild/client - CommerceProductsBillingPricingEngine Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsBillingPricingEngineModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get pricing rules (paginated) with optional filters
   */
  async getBillingPricingEngineForGetBillingPricingEngine(query?: {
    isActive?: boolean;
    ruleType?: Types.CommercePricingRuleType;
    productId?: string;
    searchTerm?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultPricingRuleDto, ApiError>> {
    const url = '/api/v1/billing/pricing-engine';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a new pricing rule
   */
  async postBillingPricingEngine(body: Types.CommerceProductsCreatePricingRuleInput): Promise<Result<Types.CommerceProductsPricingRuleDto, ApiError>> {
    const url = '/api/v1/billing/pricing-engine';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreatePricingRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Run the pricing engine for a product: base/sale price, the highest-priority applicable
   * rule (volume tiers, customer segment), and promo codes.
   */
  async postBillingPricingEngineCalculate(
    body: Types.CommerceProductsCalculatePricingInput,
  ): Promise<Result<Types.CommerceProductsPricingCalculationResult, ApiError>> {
    const url = '/api/v1/billing/pricing-engine/:calculate';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCalculatePricingInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingCalculationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a pricing rule by ID, including its volume tiers
   */
  async getBillingPricingEngineForGetBillingPricingEngineByRuleId(ruleId: string): Promise<Result<Types.CommerceProductsPricingRuleDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/${ruleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a pricing rule (full update; tiers are replaced when provided)
   */
  async putBillingPricingEngine(
    ruleId: string,
    body: Types.CommerceProductsUpdatePricingRuleInput,
  ): Promise<Result<Types.CommerceProductsPricingRuleDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/${ruleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsUpdatePricingRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a pricing rule (soft delete)
   */
  async deleteBillingPricingEngine(ruleId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/${ruleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Activate a pricing rule
   */
  async postBillingPricingEngineActivate(ruleId: string): Promise<Result<Types.CommerceProductsPricingRuleDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/${ruleId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deactivate a pricing rule
   */
  async postBillingPricingEngineDeactivate(ruleId: string): Promise<Result<Types.CommerceProductsPricingRuleDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/${ruleId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceProductsBillingPricingEngineModule(client: ApiClient): CommerceProductsBillingPricingEngineModule {
  return new CommerceProductsBillingPricingEngineModule(client);
}
