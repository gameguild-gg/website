/**
 * @game-guild/client - CommerceProductsCategories Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceProductsCategoriesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List catalog categories (optionally filtered to a subtree or the roots)
   */
  async getProductCategoriesForGetProductCategories(query?: {
    parentCategoryId?: string;
    rootsOnly?: boolean;
    includeProductCounts?: boolean;
  }): Promise<Result<Types.CommerceProductsProductCategoryDto[], ApiError>> {
    const url = '/v1/product-categories';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    return result as Result<Types.CommerceProductsProductCategoryDto[], ApiError>;
  }

  /**
   * Create a category
   */
  async postProductCategories(body: Types.CommerceProductsCreateProductCategoryInput): Promise<Result<Types.CommerceProductsProductCategoryDto, ApiError>> {
    const url = '/v1/product-categories';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsCreateProductCategoryInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductCategoryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a category by ID
   */
  async getProductCategoriesForGetProductCategoriesByCategoryId(categoryId: string): Promise<Result<Types.CommerceProductsProductCategoryDto, ApiError>> {
    const url = `/v1/product-categories/${categoryId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductCategoryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update a category (rename, re-slug, reparent, attributes)
   */
  async putProductCategories(
    categoryId: string,
    body: Types.CommerceProductsUpdateProductCategoryInput,
  ): Promise<Result<Types.CommerceProductsProductCategoryDto, ApiError>> {
    const url = `/v1/product-categories/${categoryId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceProductsUpdateProductCategoryInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceProductsProductCategoryDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a category (soft). Rejected while children or assigned products exist.
   */
  async deleteProductCategories(categoryId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/product-categories/${categoryId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createCommerceProductsCategoriesModule(client: ApiClient): CommerceProductsCategoriesModule {
  return new CommerceProductsCategoriesModule(client);
}
