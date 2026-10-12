/**
 * @game-guild/client - FeaturesCapabilities Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class FeaturesCapabilitiesModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Gets all capabilities for a tenant with their enabled states.
   * Returns a dictionary mapping capability keys to boolean enabled states.
   *
   * Example response:
   * ```json
   * {
   *   "lms.courses.basic": true,
   *   "lms.enrollments": true,
   *   "lms.certificates": false,
   *   "lxp.discovery": true,
   *   "lxp.learningPaths": false,
   *   "lxp.recommendations.basic": false,
   *   "lxp.recommendations.ai": false,
   *   "lxp.skills": false,
   *   "analytics.advanced": false,
   *   "branding.custom": false
   * }
   * ```
   */
  async getTenantsCapabilitiesForGetTenantsByTenantIdCapabilities(tenantId: string): Promise<Result<Record<string, boolean>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Record<string, boolean>, ApiError>;
  }

  /**
   * Sets or updates a capability override for a tenant.
   * Only accessible by tenant admins or platform administrators.
   */
  async postTenantsCapabilities(tenantId: string, body: Types.FeaturesSetCapabilityOverrideInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities`;

    // Validate request body
    const validatedBody = safeParse(Types.FeaturesSetCapabilityOverrideInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Checks if a specific capability is enabled for a tenant.
   */
  async getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapability(
    tenantId: string,
    capability: string,
  ): Promise<Result<Types.FeaturesCapabilityCheckOutput, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities/${capability}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.FeaturesCapabilityCheckOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Removes a capability override, reverting to the subscription plan default.
   */
  async deleteTenantsCapabilities(tenantId: string, capability: string, query?: { reason?: string }): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities/${capability}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Gets the audit log for capability changes.
   */
  async getTenantsCapabilitiesAuditLog(
    tenantId: string,
    query?: { capability?: string; fromDate?: string; toDate?: string },
  ): Promise<Result<Types.FeaturesCapabilityAuditLogDto[], ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities/audit-log`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.FeaturesCapabilityAuditLogDto[], ApiError>;
  }

  /**
   * Syncs capabilities from the tenant's current subscription plan.
   * Useful after subscription changes or plan upgrades.
   */
  async postTenantsCapabilitiesSync(tenantId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities/sync`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createFeaturesCapabilitiesModule(client: ApiClient): FeaturesCapabilitiesModule {
  return new FeaturesCapabilitiesModule(client);
}
