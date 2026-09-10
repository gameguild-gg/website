/**
 * @game-guild/client - EconomyRiskReviewAdministration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class EconomyRiskReviewAdministrationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getAdminEconomyRiskReviewsForGetAdminEconomyRiskReviews(query?: {
    status?: Types.FinanceEconomyRiskRiskReviewStatus;
    limit?: number;
    cursor?: string;
  }): Promise<Result<Types.FinanceEconomyRiskRiskReviewPage, ApiError>> {
    const url = '/api/v1/admin/economy/risk-reviews';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskRiskReviewPageSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyRiskReviewsForGetAdminEconomyRiskReviewsByReviewId(reviewId: string): Promise<Result<Types.FinanceEconomyRiskRiskReviewCase, ApiError>> {
    const url = `/api/v1/admin/economy/risk-reviews/${reviewId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskRiskReviewCaseSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyRiskReviewsApprove(
    reviewId: string,
    body: Types.APIControllersResolveEconomyRiskReviewInput,
  ): Promise<Result<Types.FinanceEconomyRiskRiskReviewCase, ApiError>> {
    const url = `/api/v1/admin/economy/risk-reviews/${reviewId}:approve`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersResolveEconomyRiskReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskRiskReviewCaseSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async postAdminEconomyRiskReviewsReject(
    reviewId: string,
    body: Types.APIControllersResolveEconomyRiskReviewInput,
  ): Promise<Result<Types.FinanceEconomyRiskRiskReviewCase, ApiError>> {
    const url = `/api/v1/admin/economy/risk-reviews/${reviewId}:reject`;

    // Validate request body
    const validatedBody = safeParse(Types.APIControllersResolveEconomyRiskReviewInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FinanceEconomyRiskRiskReviewCaseSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAdminEconomyRiskReviewsAudit(reviewId: string): Promise<Result<Array<Types.FinanceEconomyRiskRiskReviewEvent>, ApiError>> {
    const url = `/api/v1/admin/economy/risk-reviews/${reviewId}/audit`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FinanceEconomyRiskRiskReviewEvent>, ApiError>;
  }
}

export function createEconomyRiskReviewAdministrationModule(client: ApiClient): EconomyRiskReviewAdministrationModule {
  return new EconomyRiskReviewAdministrationModule(client);
}
