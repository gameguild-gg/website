/**
 * @game-guild/client - AccessControlContentTypePermissions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlContentTypePermissionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists the content-type grant rows of one tenant, optionally narrowed to one
   * content type.
   */
  async getAuthorizationContentTypesGrants(
    tenantId: string,
    query?: { contentType?: string },
  ): Promise<Result<Types.IdentityAuthorizationGetContentTypePermissionGrantsOutput, ApiError>> {
    const url = `/api/v1/authorization/content-types/${tenantId}/grants`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationGetContentTypePermissionGrantsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Soft-deletes one content-type grant row by id (tenant-isolated).
   */
  async deleteAuthorizationContentTypesGrants(tenantId: string, grantId: string): Promise<Result<void, ApiError>> {
    const url = `/api/v1/authorization/content-types/${tenantId}/grants/${grantId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Checks whether a user holds a content-type scoped permission, resolved through
   * the canonical effective-permission engine (tenant → content-type → resource,
   * DENY-WINS, deny-by-default).
   */
  async getAuthorizationContentTypesHasPermission(
    tenantId: string,
    query?: { contentType?: string; permission?: string; userId?: string },
  ): Promise<Result<Record<string, unknown>, ApiError>> {
    const url = `/api/v1/authorization/content-types/${tenantId}/has-permission`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Record<string, unknown>, ApiError>;
  }

  /**
   * Adds explicit DENY-WINS entries to a content-type grant row.
   */
  async postAuthorizationContentTypesDenies(
    body: Types.IdentityAuthorizationDenyContentTypePermissionGrantCommand,
  ): Promise<Result<Record<string, unknown>, ApiError>> {
    const url = '/api/v1/authorization/content-types/denies';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationDenyContentTypePermissionGrantCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Record<string, unknown>, ApiError>;
  }

  /**
   * Grants content-type scoped permissions to a user (null user = the tenant
   * default row for the content type).
   */
  async postAuthorizationContentTypesGrants(
    body: Types.IdentityAuthorizationGrantContentTypePermissionGrantCommand,
  ): Promise<Result<Record<string, unknown>, ApiError>> {
    const url = '/api/v1/authorization/content-types/grants';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationGrantContentTypePermissionGrantCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Record<string, unknown>, ApiError>;
  }

  /**
   * Revokes content-type scoped permissions from a user (the row is soft-deleted
   * once its payload is empty).
   */
  async postAuthorizationContentTypesGrantsRevoke(
    body: Types.IdentityAuthorizationRevokeContentTypePermissionGrantCommand,
  ): Promise<Result<Record<string, unknown>, ApiError>> {
    const url = '/api/v1/authorization/content-types/grants:revoke';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationRevokeContentTypePermissionGrantCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<Record<string, unknown>, ApiError>;
  }
}

export function createAccessControlContentTypePermissionsModule(client: ApiClient): AccessControlContentTypePermissionsModule {
  return new AccessControlContentTypePermissionsModule(client);
}
