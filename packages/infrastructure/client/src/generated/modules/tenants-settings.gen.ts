/**
 * @game-guild/client - TenantsSettings Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsSettingsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get tenant settings by tenant ID
   *
   * Retrieves comprehensive tenant settings including system configuration, feature toggles, business rules, and operational preferences.
   */
  async getTenantsSettings(tenantId: string): Promise<Result<Types.IdentityTenantsTenantSettingsDto, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace all tenant settings by tenant ID
   *
   * Replaces all tenant settings with new values. All existing settings are replaced with the provided data.
   */
  async putTenantsSettings(tenantId: string, body: Types.IdentityTenantsReplaceTenantSettingsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsReplaceTenantSettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update tenant settings by tenant ID
   *
   * Updates specific tenant settings fields without affecting other settings. Only the provided settings are modified.
   */
  async patchTenantsSettings(tenantId: string, body: Types.IdentityTenantsUpdateTenantSettingsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantSettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant feature flags
   *
   * Retrieves all feature flags configured for the tenant for experimental features and A/B testing.
   */
  async getTenantsSettingsFeatureFlags(tenantId: string): Promise<Result<Record<string, boolean>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/feature-flags`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Record<string, boolean>, ApiError>;
  }

  /**
   * Update tenant feature flags
   *
   * Updates specific feature flags for the tenant. Existing flags not specified are preserved.
   */
  async patchTenantsSettingsFeatureFlags(tenantId: string, body: Record<string, boolean>): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/feature-flags`;

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: body,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant integration settings
   *
   * Retrieves third-party integration configurations for the tenant.
   */
  async getTenantsSettingsIntegrationSettings(tenantId: string): Promise<Result<Types.IdentityTenantsTenantIntegrationSettingsDto, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/integration-settings`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantIntegrationSettingsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update tenant integration settings
   *
   * Updates third-party integration configurations for the tenant.
   */
  async patchTenantsSettingsIntegrationSettings(
    tenantId: string,
    body: Types.IdentityTenantsUpdateTenantIntegrationSettingsInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/integration-settings`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantIntegrationSettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant system limits
   *
   * Retrieves system limits and resource constraints configured for the tenant.
   */
  async getTenantsSettingsSystemLimits(tenantId: string): Promise<Result<Types.IdentityTenantsTenantSystemLimitsDto, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/system-limits`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantSystemLimitsDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Update tenant system limits
   *
   * Updates system limits and resource constraints for the tenant.
   */
  async patchTenantsSettingsSystemLimits(tenantId: string, body: Types.IdentityTenantsUpdateTenantSystemLimitsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/settings/system-limits`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantSystemLimitsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTenantsSettingsModule(client: ApiClient): TenantsSettingsModule {
  return new TenantsSettingsModule(client);
}
