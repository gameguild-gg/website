/**
 * @game-guild/client - CommercePaymentsBillingPaymentMethods Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsBillingPaymentMethodsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List registered payment methods (own methods; system admins see the whole tenant).
   *
   * Lists the live payment methods registered for the tenant. Non-admin actors see their own methods; system admins see all methods in the requested tenant.
   */
  async getBillingPaymentMethods(query?: { tenantId?: string }): Promise<Result<Types.CommercePaymentsPaymentMethodDto[], ApiError>> {
    const url = '/api/v1/billing/payment-methods';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsPaymentMethodDto[], ApiError>;
  }

  /**
   * Register a payment method from a tokenized gateway result.
   *
   * Registers a payment method from a Stripe payment method token (pm_...) produced by Stripe.js/Elements or a confirmed SetupIntent. Raw card data is never accepted; only display metadata returned by the gateway is stored.
   */
  async postBillingPaymentMethods(
    body: Types.CommercePaymentsPaymentMethodsControllerCreatePaymentMethodRequestBody,
  ): Promise<Result<Types.CommercePaymentsPaymentMethodDto, ApiError>> {
    const url = '/api/v1/billing/payment-methods';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentMethodsControllerCreatePaymentMethodRequestBodySchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentMethodDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a single payment method by ID.
   *
   * Returns a single registered payment method. Only the owner or a system administrator can read it.
   */
  async getPaymentMethodById(paymentMethodId: string): Promise<Result<Types.CommercePaymentsPaymentMethodDto, ApiError>> {
    const url = `/api/v1/billing/payment-methods/${paymentMethodId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentMethodDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update display-only fields of a payment method.
   *
   * Updates display-only fields (nickname, billing address). Card details come from the gateway and cannot be edited here.
   */
  async putBillingPaymentMethods(
    paymentMethodId: string,
    body: Types.CommercePaymentsPaymentMethodsControllerUpdatePaymentMethodRequestBody,
  ): Promise<Result<Types.CommercePaymentsPaymentMethodDto, ApiError>> {
    const url = `/api/v1/billing/payment-methods/${paymentMethodId}`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsPaymentMethodsControllerUpdatePaymentMethodRequestBodySchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentMethodDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Remove a payment method (soft delete).
   *
   * Soft-deletes a payment method. Fails closed with 409 Conflict while any active subscription still bills with it; set a new default first to re-point subscriptions.
   */
  async deleteBillingPaymentMethods(paymentMethodId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/payment-methods/${paymentMethodId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Make this payment method the default (custom action per Google API guidelines).
   *
   * Makes this payment method its owner's default inside the tenant. Enforces the single-default invariant and re-points subscriptions billed with the previous default.
   */
  async postBillingPaymentMethodsSetDefault(paymentMethodId: string): Promise<Result<Types.CommercePaymentsPaymentMethodDto, ApiError>> {
    const url = `/api/v1/billing/payment-methods/${paymentMethodId}:set-default`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentMethodDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsBillingPaymentMethodsModule(client: ApiClient): CommercePaymentsBillingPaymentMethodsModule {
  return new CommercePaymentsBillingPaymentMethodsModule(client);
}
