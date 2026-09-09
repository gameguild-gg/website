/**
 * @game-guild/client - AuthApiKeys Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthApiKeysModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List all API keys
   */
  async getAuthApiKeys(): Promise<Result<Array<Types.IdentityAuthenticationApiKeyDto>, ApiError>> {
    const url = '/v1/auth/api-keys';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Array<Types.IdentityAuthenticationApiKeyDto>, ApiError>;
  }

  /**
   * Create a new API key
   */
  async postAuthApiKeys(body: Types.IdentityAuthenticationCreateApiKeyCommand): Promise<Result<Types.IdentityAuthenticationCreateApiKeyOutput, ApiError>> {
    const url = '/v1/auth/api-keys';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCreateApiKeyCommandSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationCreateApiKeyOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke an API key
   */
  async postAuthApiKeysRevoke(keyId: string, body: Types.IdentityAuthenticationRevokeApiKeyInput): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/api-keys/${keyId}:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRevokeApiKeyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createAuthApiKeysModule(client: ApiClient): AuthApiKeysModule {
  return new AuthApiKeysModule(client);
}
