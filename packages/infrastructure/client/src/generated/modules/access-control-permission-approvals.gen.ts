/**
 * @game-guild/client - AccessControlPermissionApprovals Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AccessControlPermissionApprovalsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Creates a pending multi-stage approval workflow for a tenant permission grant.
   */
  async postAuthorizationPermissionApprovals(body: Types.IdentityAuthorizationCreatePermissionApprovalCommand): Promise<Result<string, ApiError>> {
    const url = '/api/v1/authorization/permission-approvals';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationCreatePermissionApprovalCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<string, ApiError>;
  }

  /**
   * Loads a workflow with its stages and decisions.
   */
  async getAuthorizationPermissionApprovals(id: string): Promise<Result<Types.IdentityAuthorizationPermissionApprovalWorkflow, ApiError>> {
    const url = `/api/v1/authorization/permission-approvals/${id}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionApprovalWorkflowSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Decides the workflow's current stage. On the final approval the guarded grant
   * executes; a rejection terminates the workflow without granting.
   */
  async postAuthorizationPermissionApprovalsDecisions(
    id: string,
    body: Types.IdentityAuthorizationControllersDecidePermissionApprovalInput,
  ): Promise<Result<Types.IdentityAuthorizationPermissionApprovalDecisionResult, ApiError>> {
    const url = `/api/v1/authorization/permission-approvals/${id}/decisions`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthorizationControllersDecidePermissionApprovalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthorizationPermissionApprovalDecisionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAccessControlPermissionApprovalsModule(client: ApiClient): AccessControlPermissionApprovalsModule {
  return new AccessControlPermissionApprovalsModule(client);
}
