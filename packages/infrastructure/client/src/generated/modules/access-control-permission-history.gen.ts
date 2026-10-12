/**
 * @game-guild/client - AccessControlPermissionHistory Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionHistoryModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists the append-only version history of one permission policy (issue #188).
   */
  async getAuthorizationPermissionPoliciesVersions(
    policyId: string,
    query?: { kind?: Types.IdentityAuthorizationPolicyVersionKind },
  ): Promise<Result<Types.IdentityAuthorizationPolicyVersionHistory[], ApiError>> {
    const url = `/api/v1/authorization/permission-policies/${policyId}/versions`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationPolicyVersionHistory[], ApiError>;
  }

  /**
   * Reconstructs a subject's effective permission state at a past instant
   * (issue #189: access rights at any point in time).
   */
  async getAuthorizationPermissionsHistoryAt(query?: {
    timestamp?: string;
    userId?: string;
  }): Promise<Result<Types.IdentityAuthorizationPermissionHistoryAtSnapshot, ApiError>> {
    const url = '/api/v1/authorization/permissions/history/at';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionHistoryAtSnapshotSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Lists role-inheritance change events in a window (issue #189: parent-child
   * relationship changes over time), each with the pre/post hierarchy state and
   * the cascading effects recorded at mutation time.
   */
  async getAuthorizationPermissionsHistoryInheritance(query?: {
    fromTimestamp?: string;
    toTimestamp?: string;
  }): Promise<Result<Types.IdentityAuthorizationInheritanceHistoryEvent[], ApiError>> {
    const url = '/api/v1/authorization/permissions/history/inheritance';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthorizationInheritanceHistoryEvent[], ApiError>;
  }
}

export function createAccessControlPermissionHistoryModule(client: ApiClient): AccessControlPermissionHistoryModule {
  return new AccessControlPermissionHistoryModule(client);
}
