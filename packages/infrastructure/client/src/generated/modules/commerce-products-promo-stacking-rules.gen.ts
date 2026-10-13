/**
 * @game-guild/client - CommerceProductsPromoStackingRules Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsPromoStackingRulesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List stacking rules ordered by priority (descending)
   */
  async getPromoStackingRulesForGetPromoStackingRules(query?: {
    includeInactive?: boolean;
  }): Promise<Result<Types.CommerceProductsPromoStackingRuleDto[], ApiError>> {
    const url = '/v1/promo-stacking-rules';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceProductsPromoStackingRuleDto[], ApiError>;
  }

  /**
   * Create a stacking rule
   */
  async postPromoStackingRules(
    body: Types.CommerceProductsCreatePromoStackingRuleInput,
  ): Promise<Result<Types.CommerceProductsPromoStackingRuleDto, ApiError>> {
    const url = '/v1/promo-stacking-rules';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreatePromoStackingRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoStackingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a stacking rule by ID
   */
  async getPromoStackingRulesForGetPromoStackingRulesByRuleId(ruleId: string): Promise<Result<Types.CommerceProductsPromoStackingRuleDto, ApiError>> {
    const url = `/v1/promo-stacking-rules/${ruleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoStackingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a stacking rule
   */
  async putPromoStackingRules(
    ruleId: string,
    body: Types.CommerceProductsUpdatePromoStackingRuleInput,
  ): Promise<Result<Types.CommerceProductsPromoStackingRuleDto, ApiError>> {
    const url = `/v1/promo-stacking-rules/${ruleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsUpdatePromoStackingRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoStackingRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a stacking rule
   */
  async deletePromoStackingRules(ruleId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/promo-stacking-rules/${ruleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommerceProductsPromoStackingRulesModule(client: ApiClient): CommerceProductsPromoStackingRulesModule {
  return new CommerceProductsPromoStackingRulesModule(client);
}
