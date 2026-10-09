/**
 * @game-guild/client - AccessControlPermissionRestoration Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionRestorationModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Restores a soft-deleted tenant permission row inside the retention window.
   */
  async postAuthorizationRestorationsDeleted(permissionId: string): Promise<Result<Types.IdentityAuthorizationPermissionRestorationResult, ApiError>> {
    const url = `/api/v1/authorization/restorations/deleted/${permissionId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionRestorationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Reverses a Grant/Revoke/Deny audit-log entry inside the retention window.
   */
  async postAuthorizationRestorationsUndo(auditLogId: string): Promise<Result<Types.IdentityAuthorizationPermissionRestorationResult, ApiError>> {
    const url = `/api/v1/authorization/restorations/undo/${auditLogId}`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionRestorationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionRestorationModule(client: ApiClient): AccessControlPermissionRestorationModule {
  return new AccessControlPermissionRestorationModule(client);
}
