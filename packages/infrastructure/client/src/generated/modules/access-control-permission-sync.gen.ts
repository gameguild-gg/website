/**
 * @game-guild/client - AccessControlPermissionSync Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionSyncModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Exports the permission state (roles, tenant defaults, user grants) of a tenant
   * as a synchronization document. Omit tenantId for the global
   * scope (system admins only).
   */
  async getAuthorizationSyncExport(query?: { tenantId?: string }): Promise<Result<Types.IdentityAuthorizationExternalPermissionSyncDocument, ApiError>> {
    const url = '/api/v1/authorization/sync/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationExternalPermissionSyncDocumentSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Imports (or dry-runs) an external permission synchronization document. Invalid
   * documents are rejected in full with their validation errors; nothing is
   * partially applied.
   */
  async postAuthorizationSyncImport(
    body: Types.IdentityAuthorizationImportPermissionSyncInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionSyncImportResult, ApiError>> {
    const url = '/api/v1/authorization/sync/import';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationImportPermissionSyncInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionSyncImportResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionSyncModule(client: ApiClient): AccessControlPermissionSyncModule {
  return new AccessControlPermissionSyncModule(client);
}
