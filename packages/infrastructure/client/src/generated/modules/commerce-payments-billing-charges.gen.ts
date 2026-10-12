/**
 * @game-guild/client - CommercePaymentsBillingCharges Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsBillingChargesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List billing charges
   *
   * Compatibility billing endpoint backed by the persisted payment query model.
   */
  async getBillingCharges(query?: {
    tenantId?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.CommercePaymentsPaymentResult[], ApiError>> {
    const url = '/api/v1/billing/charges';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsPaymentResult[], ApiError>;
  }

  /**
   * Create billing charge
   *
   * Processes a subscription charge through the configured payment command path.
   */
  async postBillingCharges(
    body: Types.CommercePaymentsBillingChargesControllerCreateBillingChargeInput,
  ): Promise<Result<Types.CommercePaymentsPaymentResult, ApiError>> {
    const url = '/api/v1/billing/charges';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsBillingChargesControllerCreateBillingChargeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get billing charge
   */
  async getBillingChargeById(chargeId: string): Promise<Result<Types.CommercePaymentsPaymentResult, ApiError>> {
    const url = `/api/v1/billing/charges/${chargeId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancel billing charge
   */
  async postBillingChargesCancel(
    chargeId: string,
    body: Types.CommercePaymentsBillingChargesControllerCancelBillingChargeInput,
  ): Promise<Result<Types.CommercePaymentsPaymentCancellationResult, ApiError>> {
    const url = `/api/v1/billing/charges/${chargeId}:cancel`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsBillingChargesControllerCancelBillingChargeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentCancellationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Refund billing charge
   */
  async postBillingChargesRefund(
    chargeId: string,
    body: Types.CommercePaymentsBillingChargesControllerRefundBillingChargeInput,
  ): Promise<Result<Types.CommercePaymentsProcessRefundResult, ApiError>> {
    const url = `/api/v1/billing/charges/${chargeId}:refund`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsBillingChargesControllerRefundBillingChargeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsProcessRefundResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retry billing charge
   */
  async postBillingChargesRetry(chargeId: string): Promise<Result<Types.CommercePaymentsPaymentRetryResult, ApiError>> {
    const url = `/api/v1/billing/charges/${chargeId}:retry`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsPaymentRetryResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsBillingChargesModule(client: ApiClient): CommercePaymentsBillingChargesModule {
  return new CommercePaymentsBillingChargesModule(client);
}
