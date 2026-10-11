/**
 * @game-guild/client - AuthMultiFactor Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthMultiFactorModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Get current user's MFA configuration
   *
   * Retrieves the current user's multi-factor authentication configuration and enabled methods.
   */
  async getAuthMfa(): Promise<Result<Types.IdentityAuthenticationMfaConfigurationOutput, ApiError>> {
    const url = '/v1/auth/mfa';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaConfigurationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Disable MFA for the current user
   *
   * Disables multi-factor authentication for the current user after password verification.
   */
  async postAuthMfaDisable(body: Types.IdentityAuthenticationDisableMfaInput): Promise<Result<Types.IdentityAuthenticationMfaSuccessOutput, ApiError>> {
    const url = '/v1/auth/mfa:disable';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationDisableMfaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get backup codes (masked for security)
   *
   * Retrieves the user's backup codes status. Codes are not returned for security; use regenerate to get new codes.
   */
  async getAuthMfaBackupCodes(): Promise<Result<Types.IdentityAuthenticationBackupCodesStatusOutput, ApiError>> {
    const url = '/v1/auth/mfa/backup-codes';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationBackupCodesStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Generate new backup codes (invalidates existing ones)
   *
   * Generates a new set of backup codes, invalidating any previously generated codes.
   */
  async postAuthMfaBackupCodesRegenerate(): Promise<Result<Types.IdentityAuthenticationBackupCodesOutput, ApiError>> {
    const url = '/v1/auth/mfa/backup-codes:regenerate';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationBackupCodesOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List available MFA methods
   *
   * Returns all available MFA methods and their configuration status for the current user.
   */
  async getAuthMfaMethods(): Promise<Result<Types.IdentityAuthenticationMfaMethodsOutput, ApiError>> {
    const url = '/v1/auth/mfa/methods';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaMethodsOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete SMS MFA setup by verifying the code
   *
   * Completes SMS MFA setup by verifying the code sent to the user's phone.
   */
  async postAuthMfaSmsComplete(
    body: Types.IdentityAuthenticationCompleteMfaSetupInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaSuccessOutput, ApiError>> {
    const url = '/v1/auth/mfa/sms:complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteMfaSetupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initiate SMS-based MFA setup
   *
   * Initiates SMS-based MFA setup by sending a verification code to the provided phone number.
   */
  async postAuthMfaSmsSetup(body: Types.IdentityAuthenticationSmsMfaSetupInput): Promise<Result<Types.IdentityAuthenticationSmsMfaSetupOutput, ApiError>> {
    const url = '/v1/auth/mfa/sms:setup';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationSmsMfaSetupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSmsMfaSetupOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete TOTP MFA setup by verifying the code
   *
   * Completes TOTP setup by verifying a code from the user's authenticator app.
   */
  async postAuthMfaTotpComplete(
    body: Types.IdentityAuthenticationCompleteMfaSetupInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaSuccessOutput, ApiError>> {
    const url = '/v1/auth/mfa/totp:complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteMfaSetupInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initiate TOTP MFA setup
   *
   * Initiates Time-based One-Time Password (TOTP) setup, returning a secret key and QR code URI for authenticator apps.
   */
  async postAuthMfaTotpSetup(): Promise<Result<Types.IdentityAuthenticationMfaSetupOutput, ApiError>> {
    const url = '/v1/auth/mfa/totp:setup';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSetupOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verify MFA code during authentication
   *
   * Verifies an MFA code during the authentication flow. Used after initial sign-in when MFA is required.
   */
  async postAuthMfaVerify(body: Types.IdentityAuthenticationVerifyMfaInput): Promise<Result<Types.IdentityAuthenticationMfaVerificationOutput, ApiError>> {
    const url = '/v1/auth/mfa/verify';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationVerifyMfaInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaVerificationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthMultiFactorModule(client: ApiClient): AuthMultiFactorModule {
  return new AuthMultiFactorModule(client);
}
