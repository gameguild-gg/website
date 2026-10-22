/**
 * @game-guild/client - CommercePaymentsRefunds Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsRefundsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List refunds with optional filters, newest first.
   *
   * Lists refunds with optional status and period filters, newest first. Non-admin actors are scoped to their own tenant.
   */
  async getRefunds(query?: {
    status?: Types.CommercePaymentsRefundStatus;
    fromUtc?: string;
    toUtc?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<Types.CommercePaymentsRefundListPage, ApiError>> {
    const url = '/api/v1/refunds';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundListPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Request a refund for a payment. Amounts at or above the manual-approval threshold
   * are queued as pending; amounts below it (or all refunds when no threshold is
   * configured) execute immediately.
   *
   * Records a refund request. Amounts at or above the configured manual-approval threshold enter the pending-approval queue; smaller amounts (or all refunds when no threshold is configured) execute immediately through the gateway. Use kind=prorated to refund the unused time of the subscription billing period (subscription-linked payments only).
   */
  async postRefunds(body: Types.CommercePaymentsRefundsControllerRequestRefundHttpInput): Promise<Result<Types.CommercePaymentsRequestRefundResult, ApiError>> {
    const url = '/api/v1/refunds';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRefundsControllerRequestRefundHttpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRequestRefundResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Retrieve a single refund by id.
   *
   * Retrieves a single refund with its lifecycle state, decision trail and gateway references.
   */
  async getRefundById(refundId: string): Promise<Result<Types.CommercePaymentsRefundRecord, ApiError>> {
    const url = `/api/v1/refunds/${refundId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundRecordSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Approve a pending refund and execute it. Requires the
   * `payments:refunds:approve` permission.
   *
   * Approves the pending refund and executes it through the gateway. Requires the payments:refunds:approve permission. Custom action per Google API guidelines.
   */
  async postRefundsApprove(
    refundId: string,
    body: Types.CommercePaymentsRefundsControllerRefundDecisionInput,
  ): Promise<Result<Types.CommercePaymentsRefundDecisionResult, ApiError>> {
    const url = `/api/v1/refunds/${refundId}:approve`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRefundsControllerRefundDecisionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundDecisionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reject a pending refund; no money moves. Requires the
   * `payments:refunds:approve` permission.
   *
   * Rejects the pending refund; no money moves. Requires the payments:refunds:approve permission. Custom action per Google API guidelines.
   */
  async postRefundsReject(
    refundId: string,
    body: Types.CommercePaymentsRefundsControllerRefundDecisionInput,
  ): Promise<Result<Types.CommercePaymentsRefundDecisionResult, ApiError>> {
    const url = `/api/v1/refunds/${refundId}:reject`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRefundsControllerRefundDecisionInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundDecisionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * The pending refund-approval queue, oldest first. Requires the
   * `payments:refunds:approve` permission.
   *
   * Lists refunds waiting for a manual approval decision, oldest first. Requires the payments:refunds:approve permission; non-admin actors are scoped to their own tenant.
   */
  async getRefundsPendingApprovals(query?: { page?: number; pageSize?: number }): Promise<Result<Types.CommercePaymentsRefundApprovalQueuePage, ApiError>> {
    const url = '/api/v1/refunds/pending-approvals';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundApprovalQueuePageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Refund statistics for an inclusive window.
   *
   * Returns refund counts by status, completed refund amounts by currency and the reason breakdown for an inclusive window.
   */
  async getRefundsStatistics(query?: { fromUtc?: string; toUtc?: string }): Promise<Result<Types.CommercePaymentsRefundStatistics, ApiError>> {
    const url = '/api/v1/refunds/statistics';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRefundStatisticsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsRefundsModule(client: ApiClient): CommercePaymentsRefundsModule {
  return new CommercePaymentsRefundsModule(client);
}
