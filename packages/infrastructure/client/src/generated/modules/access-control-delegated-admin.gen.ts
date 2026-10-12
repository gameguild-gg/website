/**
 * @game-guild/client - AccessControlDelegatedAdmin Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlDelegatedAdminModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Grant delegated admin scope to a user
   */
  async postDelegatedAdmin(
    body: Types.IdentityAuthorizationCommandsGrantDelegatedAdminCommand,
  ): Promise<Result<Types.IdentityAuthorizationDelegatedAdminScope, ApiError>> {
    const url = '/v1/delegated-admin';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCommandsGrantDelegatedAdminCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationDelegatedAdminScopeSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get a delegated admin scope by ID
   */
  async getDelegatedAdmin(id: string): Promise<Result<Types.IdentityAuthorizationDelegatedAdminScope, ApiError>> {
    const url = `/v1/delegated-admin/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationDelegatedAdminScopeSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke delegated admin scope
   */
  async deleteDelegatedAdmin(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/delegated-admin/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if admin can manage a resource type
   */
  async getDelegatedAdminUserCanManageResource(adminUserId: string, query?: { resourceType?: string; tenantId?: string }): Promise<Result<boolean, ApiError>> {
    const url = `/v1/delegated-admin/user/${adminUserId}/can-manage-resource`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Check if admin can manage a user
   */
  async getDelegatedAdminUserCanManageUser(adminUserId: string, targetUserId: string, query?: { tenantId?: string }): Promise<Result<boolean, ApiError>> {
    const url = `/v1/delegated-admin/user/${adminUserId}/can-manage-user/${targetUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Get managed resource types for an admin
   */
  async getDelegatedAdminUserManagedResources(adminUserId: string, query?: { tenantId?: string }): Promise<Result<string[], ApiError>> {
    const url = `/v1/delegated-admin/user/${adminUserId}/managed-resources`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<string[], ApiError>;
  }

  /**
   * Get managed users for an admin
   */
  async getDelegatedAdminUserManagedUsers(adminUserId: string, query?: { tenantId?: string }): Promise<Result<string[], ApiError>> {
    const url = `/v1/delegated-admin/user/${adminUserId}/managed-users`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<string[], ApiError>;
  }

  /**
   * Get admin scopes for a user
   */
  async getDelegatedAdminUserScopes(
    adminUserId: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.IdentityAuthorizationDelegatedAdminScope[], ApiError>> {
    const url = `/v1/delegated-admin/user/${adminUserId}/scopes`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationDelegatedAdminScope[], ApiError>;
  }
}

export function createAccessControlDelegatedAdminModule(client: ApiClient): AccessControlDelegatedAdminModule {
  return new AccessControlDelegatedAdminModule(client);
}
