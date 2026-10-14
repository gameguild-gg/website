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
   * Reset a user's multi-factor authentication: clears MFA secrets, revokes all
   * issued tokens, terminates sessions, and cancels any live lost-device recovery request.
   *
   * Disables a user's multi-factor authentication through the audited shared disable path and cancels any live recovery request. Requires system administration or tenant administration of the user's tenant.
   */
  async postAdminUsersMfaReset(userId: string): Promise<Result<Types.IdentityAuthenticationAdminResetUserMfaOutput, ApiError>> {
    const url = `/v1/admin/users/${userId}/mfa:reset`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAdminResetUserMfaOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

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
   * Disables multi-factor authentication for the current user after TOTP confirmation code verification.
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
   * Complete a recovery using the confirmation token from the second email; disables MFA.
   *
   * Consumes the single-use confirmation token and disables multi-factor authentication for the recovered account. Denials are generic and never reveal token state.
   */
  async postAuthMfaRecoveryConfirm(
    body: Types.IdentityAuthenticationMfaRecoveryConfirmInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaRecoveryConfirmOutput, ApiError>> {
    const url = '/v1/auth/mfa/recovery:confirm';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationMfaRecoveryConfirmInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaRecoveryConfirmOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Continue a recovery after the cooling-off window using the request token from the first email.
   *
   * Exchanges the recovery request token (after the cooling-off window) for a confirmation email. Denials are generic and never reveal token state.
   */
  async postAuthMfaRecoveryContinue(
    body: Types.IdentityAuthenticationMfaRecoveryContinueInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaRecoveryContinueOutput, ApiError>> {
    const url = '/v1/auth/mfa/recovery:continue';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationMfaRecoveryContinueInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaRecoveryContinueOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Start a lost-MFA-device recovery for an email address.
   *
   * Opens a lost-MFA-device recovery request for an email address and sends a recovery email with a cooling-off period. Always returns a generic success response to prevent user enumeration.
   */
  async postAuthMfaRecoveryRequest(
    body: Types.IdentityAuthenticationMfaRecoveryRequestInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaRecoveryRequestOutput, ApiError>> {
    const url = '/v1/auth/mfa/recovery:request';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationMfaRecoveryRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaRecoveryRequestOutputSchema, result.data, 'response');
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
