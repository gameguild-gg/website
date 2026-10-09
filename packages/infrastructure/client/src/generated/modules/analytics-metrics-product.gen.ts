/**
 * @game-guild/client - AnalyticsMetricsProduct Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AnalyticsMetricsProductModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Product revenue/subscription metrics are business-intelligence data:
   * access requires the monetization analytics permission (issue #346, ViewAnalytics).
   */
  async getApiMetricsProduct(query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
  }): Promise<Result<Types.AnalyticsProductMetricsOutput, ApiError>> {
    const url = '/api/metrics/product';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AnalyticsProductMetricsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Exports of revenue/subscription metrics are business-intelligence data:
   * access requires the monetization analytics permission (issue #346, ViewAnalytics).
   */
  async getApiMetricsProductExport(query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    format?: Types.AnalyticsProductMetricsExportFormat;
  }): Promise<Result<Blob, ApiError>> {
    const url = '/api/metrics/product/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }
}

export function createAnalyticsMetricsProductModule(client: ApiClient): AnalyticsMetricsProductModule {
  return new AnalyticsMetricsProductModule(client);
}
