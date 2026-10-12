/**
 * @game-guild/client - AuthSessions Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthSessionsModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get current user's active sessions
   *
   * Retrieves a list of all active sessions for the current user, including device and location information.
   */
  async getAuthSessions(): Promise<Result<Types.IdentityAuthenticationSessionOutput[], ApiError>> {
    const url = '/v1/auth/sessions';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthenticationSessionOutput[], ApiError>;
  }

  /**
   * Analyze session security
   *
   * Analyzes the current session for security risks and provides recommendations.
   */
  async getAuthSessionsAnalyzeSecurity(): Promise<Result<Types.IdentityAuthenticationSessionSecurityAnalysis, ApiError>> {
    const url = '/v1/auth/sessions:analyze-security';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionSecurityAnalysisSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Refresh the current session
   *
   * Extends the current session's expiration time.
   */
  async postAuthSessionsRefresh(): Promise<Result<Types.IdentityAuthenticationSessionSuccessOutput, ApiError>> {
    const url = '/v1/auth/sessions:refresh';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Terminate all sessions (including current)
   *
   * Terminates all active sessions including the current one. User will need to sign in again.
   */
  async postAuthSessionsTerminateAll(): Promise<Result<Types.IdentityAuthenticationSessionTerminationOutput, ApiError>> {
    const url = '/v1/auth/sessions:terminate-all';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionTerminationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Terminate all sessions except the current one
   *
   * Terminates all active sessions except the current one.
   */
  async postAuthSessionsTerminateOthers(): Promise<Result<Types.IdentityAuthenticationSessionTerminationOutput, ApiError>> {
    const url = '/v1/auth/sessions:terminate-others';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionTerminationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Terminate a specific session
   *
   * Terminates a specific session by its identifier. The session must belong to the current user.
   */
  async deleteAuthSessions(sessionId: string): Promise<Result<Types.IdentityAuthenticationSessionSuccessOutput, ApiError>> {
    const url = `/v1/auth/sessions/${sessionId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSessionSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthSessionsModule(client: ApiClient): AuthSessionsModule {
  return new AuthSessionsModule(client);
}
