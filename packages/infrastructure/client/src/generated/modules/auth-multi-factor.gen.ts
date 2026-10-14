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
   * Issue a push MFA approval challenge
   *
   * Issues a push approval challenge to the enrolled device. The challenge must be approved within the TTL; on timeout the sign-in flow falls back to TOTP.
   */
  async postAuthMfaPushChallenge(
    body: Types.IdentityAuthenticationPushMfaChallengeInput,
  ): Promise<Result<Types.IdentityAuthenticationPushMfaChallengeOutput, ApiError>> {
    const url = '/v1/auth/mfa/push:challenge';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationPushMfaChallengeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationPushMfaChallengeOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Register the mobile device for push MFA
   *
   * Registers (or replaces) the mobile device registration token used to deliver push MFA approval challenges.
   */
  async postAuthMfaPushRegisterDevice(
    body: Types.IdentityAuthenticationPushMfaDeviceInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaSuccessOutput, ApiError>> {
    const url = '/v1/auth/mfa/push:register-device';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationPushMfaDeviceInputSchema, body, 'request');

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
   * Respond to a push MFA challenge from the device
   *
   * Records the device's approve/deny decision for a pending, unexpired challenge. Only a pending challenge accepts a response; everything else fails closed.
   */
  async postAuthMfaPushRespond(body: Types.IdentityAuthenticationPushMfaRespondInput): Promise<Result<Types.IdentityAuthenticationMfaSuccessOutput, ApiError>> {
    const url = '/v1/auth/mfa/push:respond';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationPushMfaRespondInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSuccessOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Send a fresh SMS sign-in code to the enrolled phone
   *
   * Sends a fresh verification code to the enrolled phone before MFA verification. Generic failure payload; throttled to one code per minute per user.
   */
  async postAuthMfaSmsChallenge(
    body: Types.IdentityAuthenticationSmsMfaChallengeInput,
  ): Promise<Result<Types.IdentityAuthenticationSmsMfaSetupOutput, ApiError>> {
    const url = '/v1/auth/mfa/sms:challenge';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationSmsMfaChallengeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSmsMfaSetupOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete SMS MFA setup by verifying the code
   *
   * Completes SMS MFA setup by verifying the code sent to the user's phone, enrolling the verified number as an SMS factor.
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
   * Initiates SMS-based MFA setup by sending a verification code to the provided phone number. Delivery rides the SMS notification channel; codes are six digits, single-use, and expire in ten minutes.
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
