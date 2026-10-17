/**
 * @game-guild/client - AccessControlPermissionSimulation Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionSimulationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   *     Simulates a permission change (issue #374): given a subject (user, role or
   * tenant), a grant/revoke delta and an optional resource scope, returns the
   * before/after effective permission diff. Nothing is applied — no grants are
   * written, no security version is bumped, no audit entry is recorded.
   */
  async postAuthorizationPermissionsSimulate(
    body: Types.IdentityAuthorizationSimulatePermissionChangeCommand,
  ): Promise<Result<Types.IdentityAuthorizationPermissionSimulationResult, ApiError>> {
    const url = '/api/v1/authorization/permissions:simulate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationSimulatePermissionChangeCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionSimulationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionSimulationModule(client: ApiClient): AccessControlPermissionSimulationModule {
  return new AccessControlPermissionSimulationModule(client);
}
