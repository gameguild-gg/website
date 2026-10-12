/**
 * @game-guild/client - TenantsResourcesMetadata Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsResourcesMetadataModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get all metadata entries for a tenant
   *
   * Retrieves all resource metadata entries for a specific tenant, optionally filtered by category.
   */
  async getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadata(
    tenantId: string,
    query?: { category?: string },
  ): Promise<Result<Types.ResourcesResourceMetadata[], ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/metadata`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.ResourcesResourceMetadata[], ApiError>;
  }

  /**
   * Get a specific metadata entry by key
   *
   * Retrieves a specific resource metadata entry by its key for a tenant.
   */
  async getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKey(
    tenantId: string,
    key: string,
  ): Promise<Result<Types.ResourcesResourceMetadata, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/metadata/${key}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceMetadataSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Create or update a metadata entry
   *
   * Creates a new metadata entry or updates an existing one for a tenant.
   */
  async putTenantsResourcesMetadata(
    tenantId: string,
    key: string,
    body: Types.ResourcesSetResourceMetadataInput,
  ): Promise<Result<Types.ResourcesResourceMetadata, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/metadata/${key}`;

    // Validate request body
    const validatedBody = safeParse(Types.ResourcesSetResourceMetadataInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.ResourcesResourceMetadataSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a metadata entry
   *
   * Removes a resource metadata entry for a tenant.
   */
  async deleteTenantsResourcesMetadata(tenantId: string, key: string): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/resources/metadata/${key}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTenantsResourcesMetadataModule(client: ApiClient): TenantsResourcesMetadataModule {
  return new TenantsResourcesMetadataModule(client);
}
