/**
 * @game-guild/client - CommercePaymentsGifts Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsGiftsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Purchases a gift subscription for any plan and duration (1-36 months).
   *
   * Creates a purchased gift for the given recipient with a positive amount in an ISO 4217 currency, a duration of 1-36 months, an optional plan restriction, an optional personal message and an optional scheduled delivery date (redemption is blocked before it). The full redemption code is returned exactly once and only stored as a SHA-256 hash; unredeemed gifts expire one year after purchase.
   */
  async postGifts(body: Types.CommercePaymentsGiftsControllerPurchaseGiftRequestBody): Promise<Result<Types.CommercePaymentsGiftPurchaseResult, ApiError>> {
    const url = '/api/v1/gifts';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsGiftsControllerPurchaseGiftRequestBodySchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsGiftPurchaseResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Non-secret preview of a gift by redemption code (no giver identity exposed).
   *
   * Returns the public view of a gift (status, amount, currency, duration, plan, recipient name, personal message, delivery/expiry) for a redemption code. The giver identity is never exposed.
   */
  async getGifts(code: string): Promise<Result<Types.CommercePaymentsGiftPublicView, ApiError>> {
    const url = `/api/v1/gifts/${code}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsGiftPublicViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Redeems a gift with its redemption code; grants account credit to the calling account.
   *
   * Redeems the gift for the authenticated user and grants an account credit of the gift value to their account (existing or newly created accounts alike — the credit lands on whichever account redeems). Blocked before the scheduled delivery date, after the one-year expiry, for already-redeemed gifts, and when the giver tries to redeem their own gift.
   */
  async postGiftsRedeem(code: string): Promise<Result<Types.CommercePaymentsGiftPublicView, ApiError>> {
    const url = `/api/v1/gifts/${code}/redeem`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsGiftPublicViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gifts the calling user has redeemed, newest first.
   *
   * Returns a paged list of gifts redeemed by the calling user, newest first.
   */
  async getGiftsReceived(query?: { skip?: number; take?: number }): Promise<Result<Types.PagedResultGiftPublicView, ApiError>> {
    const url = '/api/v1/gifts/received';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultGiftPublicViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Gifts the calling user has sent, newest first.
   *
   * Returns a paged list of gifts purchased by the calling user, newest first, with their status machine state.
   */
  async getGiftsSent(query?: { skip?: number; take?: number }): Promise<Result<Types.PagedResultGiftPublicView, ApiError>> {
    const url = '/api/v1/gifts/sent';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultGiftPublicViewSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsGiftsModule(client: ApiClient): CommercePaymentsGiftsModule {
  return new CommercePaymentsGiftsModule(client);
}
