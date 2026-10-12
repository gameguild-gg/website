/**
 * @game-guild/client - AccessControlPermissionDelegations Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionDelegationsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Delegate permissions to another user
   */
  async postPermissionDelegations(
    body: Types.IdentityAuthorizationCommandsDelegatePermissionsCommand,
  ): Promise<Result<Types.IdentityAuthorizationPermissionDelegation, ApiError>> {
    const url = '/v1/permission-delegations';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCommandsDelegatePermissionsCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionDelegationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cleanup expired delegations (admin only)
   */
  async postPermissionDelegationsCleanup(): Promise<Result<number, ApiError>> {
    const url = '/v1/permission-delegations/:cleanup';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<number, ApiError>;
  }

  /**
   * Get a delegation by ID
   */
  async getPermissionDelegations(id: string): Promise<Result<Types.IdentityAuthorizationPermissionDelegation, ApiError>> {
    const url = `/v1/permission-delegations/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionDelegationSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke a permission delegation
   */
  async deletePermissionDelegations(id: string): Promise<Result<void, ApiError>> {
    const url = `/v1/permission-delegations/${id}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if user has a delegated permission
   */
  async getPermissionDelegationsCheck(query?: {
    delegateUserId?: string;
    permission?: string;
    tenantId?: string;
    resourceId?: string;
  }): Promise<Result<boolean, ApiError>> {
    const url = '/v1/permission-delegations/check';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<boolean, ApiError>;
  }

  /**
   * Get active delegations for a delegate user
   */
  async getPermissionDelegationsDelegate(
    delegateUserId: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.IdentityAuthorizationPermissionDelegation[], ApiError>> {
    const url = `/v1/permission-delegations/delegate/${delegateUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationPermissionDelegation[], ApiError>;
  }

  /**
   * Get delegations made by a delegator
   */
  async getPermissionDelegationsDelegator(
    delegatorUserId: string,
    query?: { tenantId?: string },
  ): Promise<Result<Types.IdentityAuthorizationPermissionDelegation[], ApiError>> {
    const url = `/v1/permission-delegations/delegator/${delegatorUserId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationPermissionDelegation[], ApiError>;
  }
}

export function createAccessControlPermissionDelegationsModule(client: ApiClient): AccessControlPermissionDelegationsModule {
  return new AccessControlPermissionDelegationsModule(client);
}
