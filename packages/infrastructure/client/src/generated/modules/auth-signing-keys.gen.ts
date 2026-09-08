/**
 * @game-guild/client - AuthSigningKeys Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthSigningKeysModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get signing keys
   *
   * Retrieves signing keys with optional status filtering. Use status=active for current signing key, status=valid for all keys usable for validation.
   */
  async getAuthSigningKeys(query?: { status?: string }): Promise<Result<Array<Types.IdentityAuthenticationJwtKeyInfoDto>, ApiError>> {
    const url = '/v1/auth/signing-keys';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Array<Types.IdentityAuthenticationJwtKeyInfoDto>, ApiError>;
  }

  /**
   * Cleanup expired keys
   *
   * Removes signing keys that have been expired beyond the retention period.
   */
  async postAuthSigningKeysCleanup(body: Types.IdentityAuthenticationCleanupKeysInput): Promise<Result<Types.IdentityAuthenticationCleanupResult, ApiError>> {
    const url = '/v1/auth/signing-keys:cleanup';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCleanupKeysInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationCleanupResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Rotate signing key
   *
   * Manually rotates to a new signing key. Previous keys remain valid for token validation during grace period.
   */
  async postAuthSigningKeysRotate(body: Types.IdentityAuthenticationRotateKeyInput): Promise<Result<Types.IdentityAuthenticationJwtKeyInfoDto, ApiError>> {
    const url = '/v1/auth/signing-keys:rotate';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRotateKeyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationJwtKeyInfoDtoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthSigningKeysModule(client: ApiClient): AuthSigningKeysModule {
  return new AuthSigningKeysModule(client);
}
