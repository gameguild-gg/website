/**
 * @game-guild/client - AccessControlPermissionGraph Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionGraphModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Builds the permission graph of the caller's tenant: users, dynamic roles, and
   * permission keys connected by assignment, inheritance, grant, and deny edges,
   * with a data-quality summary (cycles, unregistered keys, orphaned roles).
   */
  async getAuthorizationPermissionGraph(query?: {
    includeUsers?: boolean;
    format?: Types.IdentityAuthorizationGraphExportFormat;
  }): Promise<Result<Types.IdentityAuthorizationModelsPermissionGraph, ApiError>> {
    const url = '/api/v1/authorization/permission-graph';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationModelsPermissionGraphSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Simulates deleting a dynamic role and reports which users would lose permissions.
   */
  async getAuthorizationPermissionGraphRolesDeletionImpact(roleId: string): Promise<Result<Types.IdentityAuthorizationModelsRoleDeletionImpact, ApiError>> {
    const url = `/api/v1/authorization/permission-graph/roles/${roleId}/deletion-impact`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationModelsRoleDeletionImpactSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Simulates removing a permission key from a dynamic role and reports which users
   * would lose or retain the key.
   */
  async getAuthorizationPermissionGraphRolesPermissionRemovalImpact(
    roleId: string,
    permissionKey: string,
  ): Promise<Result<Types.IdentityAuthorizationModelsPermissionRemovalImpact, ApiError>> {
    const url = `/api/v1/authorization/permission-graph/roles/${roleId}/permission-removal-impact/${permissionKey}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationModelsPermissionRemovalImpactSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionGraphModule(client: ApiClient): AccessControlPermissionGraphModule {
  return new AccessControlPermissionGraphModule(client);
}
