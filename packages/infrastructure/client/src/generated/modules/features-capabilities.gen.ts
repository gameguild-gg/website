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
   */
  async getTenantsCapabilitiesAuditLog(
    tenantId: string,
    query?: { capability?: string; fromDate?: string; toDate?: string },
  ): Promise<Result<Array<Types.FeaturesCapabilityAuditLogDto>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/capabilities/audit-log`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.FeaturesCapabilityAuditLogDto>, ApiError>;
  }

  /**
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
