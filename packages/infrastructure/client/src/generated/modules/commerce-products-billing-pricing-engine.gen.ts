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
   * rule (volume tiers, customer segment, usage-metric gate), the A/B experiment arm for
   * the bucket key, and promo codes.
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

  /**
   * List pricing experiments (paginated) with optional filters.
   */
  async getBillingPricingEngineExperimentsForGetBillingPricingEngineExperiments(query?: {
    status?: Types.CommercePricingExperimentStatus;
    productId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultPricingExperimentDto, ApiError>> {
    const url = '/api/v1/billing/pricing-engine/experiments';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a pricing experiment with its arms (starts in Draft).
   */
  async postBillingPricingEngineExperiments(
    body: Types.CommerceProductsCreatePricingExperimentInput,
  ): Promise<Result<Types.CommerceProductsPricingExperimentDto, ApiError>> {
    const url = '/api/v1/billing/pricing-engine/experiments';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreatePricingExperimentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get one pricing experiment with its arms.
   */
  async getBillingPricingEngineExperimentsForGetBillingPricingEngineExperimentsByExperimentId(
    experimentId: string,
  ): Promise<Result<Types.CommerceProductsPricingExperimentDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revenue-impact analytics for one experiment over an optional window: per-variant
   * assignments, conversions, revenue, average order value and lift versus the control.
   */
  async getBillingPricingEngineExperimentsAnalytics(
    experimentId: string,
    query?: { fromUtc?: string; toUtc?: string },
  ): Promise<Result<Types.CommerceProductsPricingExperimentAnalytics, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:analytics`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentAnalyticsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolve (or create) the sticky assignment of a bucket key, returning the arm the key
   * belongs to (null arm = held out by the traffic gate).
   */
  async postBillingPricingEngineExperimentsAssign(
    experimentId: string,
    body: Types.CommerceProductsAssignExperimentInput,
  ): Promise<Result<Types.CommerceProductsPricingExperimentAssignmentResult, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:assign`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsAssignExperimentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentAssignmentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete an experiment (retains assignments and outcomes for analysis).
   */
  async postBillingPricingEngineExperimentsComplete(experimentId: string): Promise<Result<Types.CommerceProductsPricingExperimentDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:complete`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Record a monetized conversion outcome for a bucket key under its assigned arm.
   */
  async postBillingPricingEngineExperimentsConvert(
    experimentId: string,
    body: Types.CommerceProductsRecordExperimentOutcomeInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:convert`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsRecordExperimentOutcomeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Pause a Running experiment (assignments stay sticky).
   */
  async postBillingPricingEngineExperimentsPause(experimentId: string): Promise<Result<Types.CommerceProductsPricingExperimentDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:pause`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Start a Draft experiment (begins enrolling bucket keys).
   */
  async postBillingPricingEngineExperimentsStart(experimentId: string): Promise<Result<Types.CommerceProductsPricingExperimentDto, ApiError>> {
    const url = `/api/v1/billing/pricing-engine/experiments/${experimentId}:start`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingExperimentDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Run one automated pricing scheduling pass immediately (issue #395): opt-in scheduled
   * rules are activated/deactivated to match their date window and date-bounded
   * experiments start/complete. The background scheduler runs the same pass periodically.
   */
  async postBillingPricingEngineSchedulingSweep(): Promise<Result<Types.CommerceProductsPricingScheduleSweepResult, ApiError>> {
    const url = '/api/v1/billing/pricing-engine/scheduling:sweep';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPricingScheduleSweepResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceProductsBillingPricingEngineModule(client: ApiClient): CommerceProductsBillingPricingEngineModule {
  return new CommerceProductsBillingPricingEngineModule(client);
}
