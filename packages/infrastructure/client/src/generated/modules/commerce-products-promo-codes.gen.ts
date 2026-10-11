/**
 * @game-guild/client - CommerceProductsPromoCodes Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsPromoCodesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all promo codes (paginated) with optional status filter
   */
  async getPromoCodesForGetPromoCodes(query?: {
    status?: string;
    isActive?: boolean;
    type?: Types.CommerceProductsPromoCodeType;
    productId?: string;
    searchTerm?: string;
    campaignId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultPromoCodeDto, ApiError>> {
    const url = '/v1/promo-codes';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create a new promo code
   */
  async postPromoCodes(body: Types.CommerceProductsCreatePromoCodeInput): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = '/v1/promo-codes';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreatePromoCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Apply promo codes to an order. Usage is recorded atomically per applied code
   * (idempotent on the supplied key, fail-closed on usage limits).
   */
  async postPromoCodesApply(body: Types.CommerceProductsApplyPromoCodesInput): Promise<Result<Types.CommerceProductsPromoCodeApplicationResult, ApiError>> {
    const url = '/v1/promo-codes/:apply';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsApplyPromoCodesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeApplicationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk-generate promo codes with a shared prefix and random suffixes
   */
  async postPromoCodesGenerateBulk(body: Types.CommerceProductsGenerateBulkPromoCodesInput): Promise<Result<Types.CommerceProductsPromoCodeDto[], ApiError>> {
    const url = '/v1/promo-codes/:generate-bulk';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsGenerateBulkPromoCodesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceProductsPromoCodeDto[], ApiError>;
  }

  /**
   * Validate a promo code
   */
  async postPromoCodesValidate(body: Types.CommerceProductsValidatePromoCodeInput): Promise<Result<Types.CommerceProductsPromoCodeValidationResult, ApiError>> {
    const url = '/v1/promo-codes/:validate';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsValidatePromoCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeValidationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a promo code by ID
   */
  async getPromoCodesForGetPromoCodesByPromoCodeId(promoCodeId: string): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update an existing promo code (full update)
   */
  async putPromoCodes(promoCodeId: string, body: Types.CommerceProductsUpdatePromoCodeInput): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsUpdatePromoCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a promo code
   */
  async deletePromoCodes(promoCodeId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update a promo code (PATCH)
   */
  async patchPromoCodes(promoCodeId: string, body: Types.CommerceProductsPatchPromoCodeInput): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsPatchPromoCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Check if a promo code exists
   */
  async headPromoCodes(promoCodeId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Activate a promo code
   */
  async postPromoCodesActivate(promoCodeId: string): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Deactivate a promo code
   */
  async postPromoCodesDeactivate(promoCodeId: string): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get usage statistics for a promo code
   */
  async getPromoCodesUsage(promoCodeId: string): Promise<Result<Types.CommerceProductsPromoCodeUsageDto, ApiError>> {
    const url = `/v1/promo-codes/${promoCodeId}/usage`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeUsageDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a promo code by its code string
   */
  async getPromoCodesByCode(code: string): Promise<Result<Types.CommerceProductsPromoCodeDto, ApiError>> {
    const url = `/v1/promo-codes/by-code/${code}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsPromoCodeDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get aggregated campaign performance (uses, redemption rate, discount totals, top codes) within an optional window
   */
  async getPromoCodesCampaignsPerformance(
    campaignId: string,
    query?: { from?: string; to?: string },
  ): Promise<Result<Types.CommerceProductsCampaignPerformanceDto, ApiError>> {
    const url = `/v1/promo-codes/campaigns/${campaignId}/performance`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsCampaignPerformanceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceProductsPromoCodesModule(client: ApiClient): CommerceProductsPromoCodesModule {
  return new CommerceProductsPromoCodesModule(client);
}
