/**
 * @game-guild/client - AnalyticsDashboards Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AnalyticsDashboardsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async getApiAnalyticsDashboards(query?: { tenantId?: string }): Promise<Result<Array<Types.AnalyticsDashboardDto>, ApiError>> {
    const url = '/api/analytics/dashboards';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.AnalyticsDashboardDto>, ApiError>;
  }

  /**
   */
  async postApiAnalyticsDashboards(body: Types.AnalyticsCreateDashboardInput): Promise<Result<Types.AnalyticsDashboardDto, ApiError>> {
    const url = '/api/analytics/dashboards';

    // Validate request body
    const validatedBody = safeParse(Types.AnalyticsCreateDashboardInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AnalyticsDashboardDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getAnalyticsDashboardById(id: string): Promise<Result<Types.AnalyticsDashboardDto, ApiError>> {
    const url = `/api/analytics/dashboards/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AnalyticsDashboardDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async putApiAnalyticsDashboards(id: string, body: Types.AnalyticsUpdateDashboardInput): Promise<Result<Types.AnalyticsDashboardDto, ApiError>> {
    const url = `/api/analytics/dashboards/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.AnalyticsUpdateDashboardInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AnalyticsDashboardDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAnalyticsDashboardsModule(client: ApiClient): AnalyticsDashboardsModule {
  return new AnalyticsDashboardsModule(client);
}
