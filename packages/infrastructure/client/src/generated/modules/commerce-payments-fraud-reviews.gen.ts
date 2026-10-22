/**
 * @game-guild/client - CommercePaymentsFraudReviews Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsFraudReviewsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List the pending manual fraud reviews, oldest first.
   *
   * Lists payments held by the fraud gate with a pending manual review, oldest first. Supports filtering by minimum score, risk band, charged amount and creation date range. Requires the payments:admin permission.
   */
  async getFraudReviews(query?: {
    tenantId?: string;
    page?: number;
    pageSize?: number;
    minScore?: number;
    riskLevel?: Types.CommercePaymentsFraudRiskLevel;
    minAmount?: number;
    fromUtc?: string;
    toUtc?: string;
  }): Promise<Result<Types.CommercePaymentsFraudReviewListItem[], ApiError>> {
    const url = '/api/v1/fraud/reviews';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.CommercePaymentsFraudReviewListItem[], ApiError>;
  }

  /**
   * Approve a pending fraud review, releasing the fraud hold so the payment may be retried.
   *
   * Approves the fraud review for a held payment, releasing the fraud hold so the payment can be retried through the manual retry flow. Requires the payments:admin permission.
   */
  async postFraudReviewsApprove(
    paymentId: string,
    body: Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewInput,
  ): Promise<Result<Types.CommercePaymentsFraudReviewResolutionResult, ApiError>> {
    const url = `/api/v1/fraud/reviews/${paymentId}:approve`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFraudReviewResolutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Decline a pending fraud review, keeping the payment failed and non-retryable.
   *
   * Declines the fraud review for a held payment; it stays failed and is never retried. Requires the payments:admin permission.
   */
  async postFraudReviewsDeny(
    paymentId: string,
    body: Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewInput,
  ): Promise<Result<Types.CommercePaymentsFraudReviewResolutionResult, ApiError>> {
    const url = `/api/v1/fraud/reviews/${paymentId}:deny`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFraudReviewResolutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk-approve pending fraud reviews, releasing the fraud holds.
   *
   * Approves the pending fraud reviews for a batch of held payments (2-100), releasing each fraud hold so the payment can be retried. Items that are not pending reviews are skipped with a reason. Requires the payments:admin permission.
   */
  async postFraudReviewsBulkApprove(
    body: Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewsBulkInput,
  ): Promise<Result<Types.CommercePaymentsFraudReviewsBulkResolutionResult, ApiError>> {
    const url = '/api/v1/fraud/reviews/bulk-approve';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewsBulkInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFraudReviewsBulkResolutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Bulk-decline pending fraud reviews, keeping the payments failed and non-retryable.
   *
   * Declines the pending fraud reviews for a batch of held payments (2-100); each stays failed and is never retried. Items that are not pending reviews are skipped with a reason. Requires the payments:admin permission.
   */
  async postFraudReviewsBulkDeny(
    body: Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewsBulkInput,
  ): Promise<Result<Types.CommercePaymentsFraudReviewsBulkResolutionResult, ApiError>> {
    const url = '/api/v1/fraud/reviews/bulk-deny';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsFraudReviewsControllerResolveFraudReviewsBulkInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsFraudReviewsBulkResolutionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsFraudReviewsModule(client: ApiClient): CommercePaymentsFraudReviewsModule {
  return new CommercePaymentsFraudReviewsModule(client);
}
