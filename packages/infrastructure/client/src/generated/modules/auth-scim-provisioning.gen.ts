/**
 * @game-guild/client - AuthScimProvisioning Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthScimProvisioningModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Lists the tenant's provisioning tokens (no plaintext).
   */
  async getAuthScimProvisioningTokens(): Promise<Result<Types.IdentityProvisioningScimProvisioningTokenDto[], ApiError>> {
    const url = '/v1/auth/scim-provisioning-tokens';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityProvisioningScimProvisioningTokenDto[], ApiError>;
  }

  /**
   * Issues a new tenant-scoped provisioning token. The plaintext is returned once.
   */
  async postAuthScimProvisioningTokens(
    body: Types.IdentityProvisioningCreateScimProvisioningTokenInput,
  ): Promise<Result<Types.IdentityProvisioningCreateScimProvisioningTokenOutput, ApiError>> {
    const url = '/v1/auth/scim-provisioning-tokens';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningCreateScimProvisioningTokenInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningCreateScimProvisioningTokenOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revokes a provisioning token immediately.
   */
  async postAuthScimProvisioningTokensRevoke(
    tokenId: string,
    body: Types.IdentityProvisioningRevokeScimProvisioningTokenInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/scim-provisioning-tokens/${tokenId}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningRevokeScimProvisioningTokenInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Rotates a provisioning token; the old token stays valid for the grace window.
   */
  async postAuthScimProvisioningTokensRotate(
    tokenId: string,
    body: Types.IdentityProvisioningRotateScimProvisioningTokenInput,
  ): Promise<Result<Types.IdentityProvisioningRotateScimProvisioningTokenOutput, ApiError>> {
    const url = `/v1/auth/scim-provisioning-tokens/${tokenId}:rotate`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityProvisioningRotateScimProvisioningTokenInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityProvisioningRotateScimProvisioningTokenOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthScimProvisioningModule(client: ApiClient): AuthScimProvisioningModule {
  return new AuthScimProvisioningModule(client);
}
