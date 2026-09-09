/**
 * @game-guild/client - CommerceSubscriptionsReportsSubscriptions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommerceSubscriptionsReportsSubscriptionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get subscription churn and retention report
   *
   * Calculates churn, retention, MRR, and subscription status breakdown for the selected period.
   */
  async getReportsChurn(query?: {
    tenantId?: string;
    startDate?: string;
    endDate?: string;
  }): Promise<Result<Types.CommerceSubscriptionsSubscriptionChurnReportDto, ApiError>> {
    const url = '/api/v1/reports/churn';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommerceSubscriptionsSubscriptionChurnReportDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommerceSubscriptionsReportsSubscriptionsModule(client: ApiClient): CommerceSubscriptionsReportsSubscriptionsModule {
  return new CommerceSubscriptionsReportsSubscriptionsModule(client);
}
