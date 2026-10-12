/**
 * @game-guild/client - CommerceOrders Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceOrdersModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List orders with optional filtering.
   * Use owner=me to get current user's orders.
   * Admin users can list all orders without owner filter.
   */
  async getOrdersForGetOrders(query?: { owner?: string; status?: Types.CommerceOrdersOrderStatus }): Promise<Result<Types.CommerceOrdersOrderDto[], ApiError>> {
    const url = '/v1/orders';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommerceOrdersOrderDto[], ApiError>;
  }

  /**
   * Create a new order with idempotency protection
   */
  async postOrders(body: Types.CommerceOrdersCreateOrderInput): Promise<Result<Types.CommerceOrdersOrderDto, ApiError>> {
    const url = '/v1/orders';

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersCreateOrderInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersOrderDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get an order by ID
   */
  async getOrdersForGetOrdersByOrderId(orderId: string): Promise<Result<Types.CommerceOrdersOrderDto, ApiError>> {
    const url = `/v1/orders/${orderId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersOrderDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Capture payment for an authorized order
   */
  async postOrdersCapture(orderId: string, body: Types.CommerceOrdersCaptureOrderInput): Promise<Result<Types.CommerceOrdersOrderCaptureDto, ApiError>> {
    const url = `/v1/orders/${orderId}:capture`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersCaptureOrderInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersOrderCaptureDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete an order (process payment, grant entitlements)
   */
  async postOrdersComplete(orderId: string, body: Types.CommerceOrdersCompleteOrderInput): Promise<Result<Types.CommerceOrdersOrderDto, ApiError>> {
    const url = `/v1/orders/${orderId}:complete`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersCompleteOrderInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersOrderDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postOrdersPaymentIntent(orderId: string): Promise<Result<Types.CommerceOrderPaymentIntentPreparation, ApiError>> {
    const url = `/v1/orders/${orderId}:payment-intent`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrderPaymentIntentPreparationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Add a product to an existing order
   */
  async postOrdersItems(orderId: string, body: Types.CommerceOrdersAddOrderItemInput): Promise<Result<Types.CommerceOrdersOrderDto, ApiError>> {
    const url = `/v1/orders/${orderId}/items`;

    // Validate request body
    const validatedBody = safeParse(Types.CommerceOrdersAddOrderItemInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceOrdersOrderDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceOrdersModule(client: ApiClient): CommerceOrdersModule {
  return new CommerceOrdersModule(client);
}
