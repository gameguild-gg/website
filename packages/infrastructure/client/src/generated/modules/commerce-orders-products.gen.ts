/**
 * @game-guild/client - CommerceOrdersProducts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceOrdersProductsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get rule-based recommendations for a product
   */
  async getProductsRecommendations(productId: string, query?: { take?: number }): Promise<Result<Types.CommerceOrdersProductRecommendationsDto, ApiError>> {
    const url = `/v1/products/${productId}/recommendations`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersProductRecommendationsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceOrdersProductsModule(client: ApiClient): CommerceOrdersProductsModule {
  return new CommerceOrdersProductsModule(client);
}
