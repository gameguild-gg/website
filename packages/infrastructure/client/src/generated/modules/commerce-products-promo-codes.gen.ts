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
   */
  async getPromoCodesForGetPromoCodes(query?: {
    status?: string;
    isActive?: boolean;
    type?: Types.CommerceProductsPromoCodeType;
    productId?: string;
    searchTerm?: string;
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
}

export function createCommerceProductsPromoCodesModule(client: ApiClient): CommerceProductsPromoCodesModule {
  return new CommerceProductsPromoCodesModule(client);
}
