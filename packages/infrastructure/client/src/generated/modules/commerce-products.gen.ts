/**
 * @game-guild/client - CommerceProducts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getProductsForGetProducts(query?: {
    type?: Types.CommerceProductsProductType;
    creatorId?: string;
    searchTerm?: string;
    isBundle?: boolean;
    includeUnpublished?: boolean;
    skip?: number;
    take?: number;
    sortBy?: string;
    sortDirection?: string;
  }): Promise<Result<Types.PagedResultProductDto, ApiError>> {
    const url = '/v1/products';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProducts(body: Types.CommerceProductsCreateProductInput): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = '/v1/products';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreateProductInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProductsBatchCreate(body: Types.CommerceProductsBatchCreateProductsInput): Promise<Result<Array<Types.CommerceProductsProductDto>, ApiError>> {
    const url = '/v1/products/:batch-create';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsBatchCreateProductsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Array<Types.CommerceProductsProductDto>, ApiError>;
  }

  /**
   */
  async getProductsForGetProductsByProductId(
    productId: string,
    query?: { includePricing?: boolean; includeUnpublished?: boolean },
  ): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putProducts(productId: string, body: Types.CommerceProductsUpdateProductInput): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsUpdateProductInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteProducts(productId: string, query?: { softDelete?: boolean; reason?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/products/${productId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async patchProducts(productId: string, body: Types.CommerceProductsPatchProductInput): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsPatchProductInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async headProducts(productId: string, query?: { includeUnpublished?: boolean }): Promise<Result<void, ApiError>> {
    const url = `/v1/products/${productId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postProductsActivate(productId: string): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProductsArchive(productId: string): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}:archive`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postProductsDeactivate(productId: string): Promise<Result<Types.CommerceProductsProductDto, ApiError>> {
    const url = `/v1/products/${productId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getProductsPricing(
    productId: string,
    query?: { includeUnpublished?: boolean },
  ): Promise<Result<Array<Types.CommerceProductsProductPricingDto>, ApiError>> {
    const url = `/v1/products/${productId}/pricing`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.CommerceProductsProductPricingDto>, ApiError>;
  }

  /**
   */
  async putProductsPricing(
    productId: string,
    body: Types.CommerceProductsSetProductPricingInput,
  ): Promise<Result<Types.CommerceProductsProductPricingDto, ApiError>> {
    const url = `/v1/products/${productId}/pricing`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsSetProductPricingInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductPricingDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceProductsModule(client: ApiClient): CommerceProductsModule {
  return new CommerceProductsModule(client);
}
