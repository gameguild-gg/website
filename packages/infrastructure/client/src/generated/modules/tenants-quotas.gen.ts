/**
 * @game-guild/client - TenantsQuotas Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsQuotasModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all quotas for a tenant
   *
   * Retrieves all configured resource quotas for a specific tenant organization.
   */
  async getTenantsQuotasForGetTenantsByTenantIdQuotas(tenantId: string): Promise<Result<Types.ResourcesResourceQuotaOutput[], ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceQuotaOutput[], ApiError>;
  }

  /**
   * Get specific quota for a resource type
   *
   * Retrieves the quota configuration for a specific resource type for a tenant.
   */
  async getTenantsQuotasForGetTenantsByTenantIdQuotasByType(
    tenantId: string,
    type: Types.ResourcesResourceUsageType,
  ): Promise<Result<Types.ResourcesResourceQuotaOutput, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceQuotaOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Set or update a quota for a resource type
   *
   * Creates or updates the quota configuration for a specific resource type for a tenant.
   */
  async putTenantsQuotas(tenantId: string, type: Types.ResourcesResourceUsageType, body: Types.ResourcesSetQuotaInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Delete a quota for a resource type
   *
   * Removes the quota configuration for a specific resource type for a tenant.
   */
  async deleteTenantsQuotas(tenantId: string, type: Types.ResourcesResourceUsageType): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if a usage amount would exceed quota
   *
   * Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.
   */
  async postTenantsQuotasCheck(
    tenantId: string,
    type: Types.ResourcesResourceUsageType,
    body: Types.ResourcesCheckResourceQuotaInput,
  ): Promise<Result<Types.ResourcesResourceQuotaEnforcementResult, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}:check`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesCheckResourceQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceQuotaEnforcementResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reset quota usage to zero
   *
   * Resets the current usage counter for a specific resource quota to zero without changing the quota limits.
   */
  async postTenantsQuotasReset(tenantId: string, type: Types.ResourcesResourceUsageType): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Toggle quota activation status
   *
   * Activates or deactivates a resource quota. Inactive quotas are not enforced.
   */
  async postTenantsQuotasToggle(
    tenantId: string,
    type: Types.ResourcesResourceUsageType,
    body: Types.ResourcesToggleResourceQuotaInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/quotas/${type}:toggle`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesToggleResourceQuotaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTenantsQuotasModule(client: ApiClient): TenantsQuotasModule {
  return new TenantsQuotasModule(client);
}
