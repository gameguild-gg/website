/**
 * @game-guild/client - CommercePaymentsTaxRules Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsTaxRulesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get tax rules for a jurisdiction
   */
  async getTaxRulesForGetTaxRules(query?: {
    jurisdictionCode?: string;
    customerType?: string;
    effectiveDate?: string;
  }): Promise<Result<Types.CommercePaymentsTaxRate[], ApiError>> {
    const url = '/api/v1/tax-rules';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsTaxRate[], ApiError>;
  }

  /**
   * Create a new tax rule
   *
   * Creates a new tax rule with the provided information.
   */
  async postTaxRules(body: Types.CommercePaymentsCreateTaxRuleInput): Promise<Result<void, ApiError>> {
    const url = '/api/v1/tax-rules';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsCreateTaxRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tax rule by ID
   *
   * Retrieves detailed information for a specific tax rule.
   */
  async getTaxRulesForGetTaxRulesByRuleId(ruleId: string): Promise<Result<Types.CommercePaymentsTaxRuleDto, ApiError>> {
    const url = `/api/v1/tax-rules/${ruleId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsTaxRuleDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a tax rule
   *
   * Deletes a tax rule by ID.
   */
  async deleteTaxRules(ruleId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/tax-rules/${ruleId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update a tax rule
   *
   * Updates specific fields of a tax rule.
   */
  async patchTaxRules(ruleId: string, body: Types.CommercePaymentsPatchTaxRuleInput): Promise<Result<void, ApiError>> {
    const url = `/api/v1/tax-rules/${ruleId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPatchTaxRuleInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommercePaymentsTaxRulesModule(client: ApiClient): CommercePaymentsTaxRulesModule {
  return new CommercePaymentsTaxRulesModule(client);
}
