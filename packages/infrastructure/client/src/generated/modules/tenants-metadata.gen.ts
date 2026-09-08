/**
 * @game-guild/client - TenantsMetadata Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class TenantsMetadataModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get tenant metadata by tenant ID
   *
   * Retrieves comprehensive tenant metadata including custom fields, tags, external references, and business information.
   */
  async getTenantsMetadata(tenantId: string): Promise<Result<Types.IdentityTenantsTenantMetadataDto, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityTenantsTenantMetadataDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Replace all tenant metadata by tenant ID
   *
   * Replaces all tenant metadata with new values. All existing metadata is replaced with the provided data.
   */
  async putTenantsMetadata(tenantId: string, body: Types.IdentityTenantsReplaceTenantMetadataInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsReplaceTenantMetadataInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Partially update tenant metadata by tenant ID
   *
   * Updates specific tenant metadata fields without affecting other metadata. Only the provided metadata keys are modified.
   */
  async patchTenantsMetadata(tenantId: string, body: Types.IdentityTenantsUpdateTenantMetadataInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantMetadataInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant custom fields
   *
   * Retrieves all custom fields configured for the tenant as a key-value dictionary for storing tenant-specific data.
   */
  async getTenantsMetadataCustomFields(tenantId: string): Promise<Result<Record<string, Record<string, unknown>>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata/custom-fields`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Record<string, Record<string, unknown>>, ApiError>;
  }

  /**
   * Update tenant custom fields
   *
   * Updates specific custom fields for the tenant. Existing fields not specified are preserved.
   */
  async patchTenantsMetadataCustomFields(tenantId: string, body: Record<string, Record<string, unknown>>): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata/custom-fields`;

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: body,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Get tenant tags
   *
   * Retrieves all tags configured for the tenant for categorization and filtering purposes.
   */
  async getTenantsMetadataTags(tenantId: string): Promise<Result<Array<string>, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata/tags`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<string>, ApiError>;
  }

  /**
   * Replace all tenant tags
   *
   * Replaces all existing tags with the provided list of tags.
   */
  async putTenantsMetadataTags(tenantId: string, body: Array<string>): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata/tags`;

    const result = await this.client.request({
      method: 'PUT',
      path: url,
      body: body,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update tenant tags
   *
   * Updates the tags for the tenant. Existing tags are merged with the new tags.
   */
  async patchTenantsMetadataTags(tenantId: string, body: Types.IdentityTenantsUpdateTenantTagsInput): Promise<Result<void, ApiError>> {
    const url = `/v1/tenants/${tenantId}/metadata/tags`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityTenantsUpdateTenantTagsInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createTenantsMetadataModule(client: ApiClient): TenantsMetadataModule {
  return new TenantsMetadataModule(client);
}
