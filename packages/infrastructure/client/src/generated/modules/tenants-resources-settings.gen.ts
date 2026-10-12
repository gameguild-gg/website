/**
 * @game-guild/client - TenantsResourcesSettings Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsResourcesSettingsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all settings for a tenant
   *
   * Retrieves all resource settings for a specific tenant, optionally filtered by category.
   */
  async getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettings(
    tenantId: string,
    query?: { category?: string },
  ): Promise<Result<Types.ResourcesResourceSettings[], ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/settings`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceSettings[], ApiError>;
  }

  /**
   * Get a specific setting by key
   *
   * Retrieves a specific resource setting by its key for a tenant.
   */
  async getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKey(
    tenantId: string,
    key: string,
  ): Promise<Result<Types.ResourcesResourceSettings, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/settings/${key}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceSettingsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or update a setting
   *
   * Creates a new setting or updates an existing one for a tenant.
   */
  async putTenantsResourcesSettings(
    tenantId: string,
    key: string,
    body: Types.ResourcesSetResourceSettingsInput,
  ): Promise<Result<Types.ResourcesResourceSettings, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/settings/${key}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetResourceSettingsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceSettingsSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a setting
   *
   * Removes a resource setting for a tenant.
   */
  async deleteTenantsResourcesSettings(tenantId: string, key: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/settings/${key}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get effective value for a setting (considering user overrides)
   *
   * Retrieves the effective value for a setting, considering user-level overrides if a user ID is provided.
   */
  async getTenantsResourcesSettingsEffective(
    tenantId: string,
    key: string,
    query?: { userId?: string },
  ): Promise<Result<Types.ResourcesEffectiveSettingOutput, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/settings/${key}/effective`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesEffectiveSettingOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createTenantsResourcesSettingsModule(client: ApiClient): TenantsResourcesSettingsModule {
  return new TenantsResourcesSettingsModule(client);
}
