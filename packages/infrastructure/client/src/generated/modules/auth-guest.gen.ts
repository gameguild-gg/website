/**
 * @game-guild/client - AuthGuest Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthGuestModule {
  constructor(private readonly client: ApiClient) {}

  /**
   *     Introspect the caller's current guest session: guest id, role and the enumerated
   * permissions it holds. Served only on the guest scheme — a user access token is not a
   * valid credential here, and a guest token is not valid anywhere else.
   *
   * Returns the caller's guest session id, role and the enumerated guest permissions. Requires the guest bearer credential.
   */
  async getAuthGuestSession(): Promise<Result<Types.IdentityAuthenticationGuestSessionStatus, ApiError>> {
    const url = '/v1/auth/guest-session';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationGuestSessionStatusSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   *     Issue a short-lived anonymous guest session. No credentials are presented or
   * returned beyond the guest token itself; there is no refresh token by design.
   *
   * Issues a short-lived guest session (guest audience bearer token, no refresh credential) holding only the enumerated public guest permissions. Rate limited per client address and audited through the security-event pipeline.
   */
  async postAuthGuestSession(
    body: Types.IdentityAuthenticationGuestSessionIssueInput,
  ): Promise<Result<Types.IdentityAuthenticationGuestSessionOutput, ApiError>> {
    const url = '/v1/auth/guest-session';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationGuestSessionIssueInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationGuestSessionOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthGuestModule(client: ApiClient): AuthGuestModule {
  return new AuthGuestModule(client);
}
