/**
 * @game-guild/client - MonitoringSla Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class MonitoringSlaModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Record a service level indicator metric.
   */
  async postSlaSlis(body: Types.MonitoringSLARecordSliMetricCommand): Promise<Result<void, ApiError>> {
    const url = '/api/v1/sla/slis';

    // Validate request body
    const validatedBody = safeParse(Types.MonitoringSLARecordSliMetricCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get all service level objectives with optional filtering.
   */
  async getSlaSlosForGetSlaSlos(query?: {
    tenantId?: string;
    serviceName?: string;
    isEnabled?: boolean;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.MonitoringSLASloDto[], ApiError>> {
    const url = '/api/v1/sla/slos';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.MonitoringSLASloDto[], ApiError>;
  }

  /**
   * Create a new service level objective.
   */
  async postSlaSlos(body: Types.MonitoringSLACreateSloCommand): Promise<Result<Types.MonitoringSLASloDto, ApiError>> {
    const url = '/api/v1/sla/slos';

    // Validate request body
    const validatedBody = safeParse(Types.MonitoringSLACreateSloCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.MonitoringSLASloDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a service level objective by ID.
   */
  async getSlaSlosForGetSlaSlosById(id: string): Promise<Result<Types.MonitoringSLASloDto, ApiError>> {
    const url = `/api/v1/sla/slos/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.MonitoringSLASloDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update an existing service level objective.
   */
  async putSlaSlos(id: string, body: Types.MonitoringSLAUpdateSloCommand): Promise<Result<Types.MonitoringSLASloDto, ApiError>> {
    const url = `/api/v1/sla/slos/${id}`;

    // Validate request body
    const validatedBody = safeParse(Types.MonitoringSLAUpdateSloCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.MonitoringSLASloDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a service level objective.
   */
  async deleteSlaSlos(id: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/sla/slos/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get compliance information for an SLO over a time period.
   */
  async getSlaSlosCompliance(id: string, query?: { startDate?: string; endDate?: string }): Promise<Result<Types.MonitoringSLASloComplianceDto, ApiError>> {
    const url = `/api/v1/sla/slos/${id}/compliance`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.MonitoringSLASloComplianceDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get error budget information for an SLO.
   */
  async getSlaSlosErrorBudget(id: string): Promise<Result<Types.MonitoringSLAErrorBudgetDto, ApiError>> {
    const url = `/api/v1/sla/slos/${id}/error-budget`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.MonitoringSLAErrorBudgetDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get SLO violations with optional filtering.
   */
  async getSlaViolations(query?: {
    sloId?: string;
    tenantId?: string;
    onlyUnresolved?: boolean;
    startDate?: string;
    endDate?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.MonitoringSLASloViolationDto[], ApiError>> {
    const url = '/api/v1/sla/violations';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.MonitoringSLASloViolationDto[], ApiError>;
  }

  /**
   * Resolve an SLO violation.
   */
  async postSlaViolationsResolve(id: string, body: Types.MonitoringSLAResolveSloViolationCommand): Promise<Result<void, ApiError>> {
    const url = `/api/v1/sla/violations/${id}/resolve`;

    // Validate request body
    const validatedBody = safeParse(Types.MonitoringSLAResolveSloViolationCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createMonitoringSlaModule(client: ApiClient): MonitoringSlaModule {
  return new MonitoringSlaModule(client);
}
