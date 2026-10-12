/**
 * @game-guild/client - AuthServiceAccountsTokens Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthServiceAccountsTokensModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * OAuth2 client_credentials grant - authenticates a service account and returns a JWT token.
   *
   * This endpoint implements the OAuth2 client_credentials flow for machine-to-machine authentication.
   * Authentication uses the `client_secret` form field, or an X.509 client certificate
   * negotiated over TLS in lieu of the secret when one is bound to the service account.
   * The returned access token can be used to authenticate API requests.
   */
  async postOauthToken(): Promise<Result<Types.IdentityAuthenticationClientCredentialsTokenOutput, ApiError>> {
    const url = '/v1/oauth/token';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationClientCredentialsTokenOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthServiceAccountsTokensModule(client: ApiClient): AuthServiceAccountsTokensModule {
  return new AuthServiceAccountsTokensModule(client);
}
