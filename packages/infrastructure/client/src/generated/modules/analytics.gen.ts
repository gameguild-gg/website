/**
 * @game-guild/client - Analytics Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AnalyticsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   */
  async postApiAnalyticsEvents(body: Types.AnalyticsTrackAnalyticsEventCommand): Promise<Result<void, ApiError>> {
    const url = '/api/analytics/events';

    // Validate request body
    const validatedBody = safeParse(Types.AnalyticsTrackAnalyticsEventCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async postApiAnalyticsFunnel(body: Types.AnalyticsAnalyzeFunnelQuery): Promise<Result<void, ApiError>> {
    const url = '/api/analytics/funnel';

    // Validate request body
    const validatedBody = safeParse(Types.AnalyticsAnalyzeFunnelQuerySchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiAnalyticsKpi(kpiName: string, query?: { startDate?: string; endDate?: string; tenantId?: string }): Promise<Result<void, ApiError>> {
    const url = `/api/analytics/kpi/${kpiName}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiAnalyticsPlatformKpis(): Promise<Result<Types.APIControllersPlatformKpisOutput, ApiError>> {
    const url = '/api/analytics/platform-kpis';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.APIControllersPlatformKpisOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   */
  async getApiAnalyticsTimeseries(query?: {
    eventName?: string;
    startDate?: string;
    endDate?: string;
    granularity?: Types.AnalyticsTimeSeriesGranularity;
    tenantId?: string;
  }): Promise<Result<void, ApiError>> {
    const url = '/api/analytics/timeseries';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiAnalyticsWarehouseExport(query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    factName?: string;
    take?: number;
  }): Promise<Result<void, ApiError>> {
    const url = '/api/analytics/warehouse/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   */
  async getApiAnalyticsWarehouseFacts(query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    factName?: string;
    take?: number;
  }): Promise<Result<Array<Types.AnalyticsAnalyticsWarehouseFactDto>, ApiError>> {
    const url = '/api/analytics/warehouse/facts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.AnalyticsAnalyticsWarehouseFactDto>, ApiError>;
  }

  /**
   */
  async postApiAnalyticsWarehouseRun(body: Types.AnalyticsAnalyticsWarehouseRunInput): Promise<Result<Types.AnalyticsAnalyticsWarehouseRunOutput, ApiError>> {
    const url = '/api/analytics/warehouse/run';

    // Validate request body
    const validatedBody = safeParse(Types.AnalyticsAnalyticsWarehouseRunInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.AnalyticsAnalyticsWarehouseRunOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAnalyticsModule(client: ApiClient): AnalyticsModule {
  return new AnalyticsModule(client);
}
