/**
 * @game-guild/client - AccessControlPermissionCompliance Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionComplianceModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Builds the permission effectiveness compliance report (overall and per
   * permission / evaluation surface / operation allow-deny rates) for a time range.
   */
  async getAuthorizationComplianceReport(query?: {
    tenantId?: string;
    fromUtc?: string;
    toUtc?: string;
  }): Promise<Result<Types.IdentityAuthorizationPermissionComplianceReport, ApiError>> {
    const url = '/api/v1/authorization/compliance/report';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionComplianceReportSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionComplianceModule(client: ApiClient): AccessControlPermissionComplianceModule {
  return new AccessControlPermissionComplianceModule(client);
}
