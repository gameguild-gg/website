/**
 * @game-guild/client - CommerceMarketplaceCart Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceMarketplaceCartModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getVMarketplaceCart(version: string): Promise<Result<Types.CommerceOrdersMarketplaceCartDto, ApiError>> {
    const url = `/v${version}/marketplace/cart`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersMarketplaceCartDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postVMarketplaceCartCheckout(
    version: string,
    body: Types.CommerceOrdersCheckoutMarketplaceCartInput,
  ): Promise<Result<Types.CommerceOrdersMarketplaceCheckoutDto, ApiError>> {
    const url = `/v${version}/marketplace/cart/checkout`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersCheckoutMarketplaceCartInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersMarketplaceCheckoutDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postVMarketplaceCartItems(
    version: string,
    body: Types.CommerceOrdersAddMarketplaceCartItemInput,
  ): Promise<Result<Types.CommerceOrdersMarketplaceCartDto, ApiError>> {
    const url = `/v${version}/marketplace/cart/items`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersAddMarketplaceCartItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersMarketplaceCartDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async deleteVMarketplaceCartItems(
    itemId: string,
    version: string,
    query?: { expectedVersion?: number },
  ): Promise<Result<Types.CommerceOrdersMarketplaceCartDto, ApiError>> {
    const url = `/v${version}/marketplace/cart/items/${itemId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersMarketplaceCartDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async patchVMarketplaceCartItems(
    itemId: string,
    version: string,
    body: Types.CommerceOrdersSetMarketplaceCartItemQuantityInput,
  ): Promise<Result<Types.CommerceOrdersMarketplaceCartDto, ApiError>> {
    const url = `/v${version}/marketplace/cart/items/${itemId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersSetMarketplaceCartItemQuantityInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersMarketplaceCartDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceMarketplaceCartModule(client: ApiClient): CommerceMarketplaceCartModule {
  return new CommerceMarketplaceCartModule(client);
}
