/**
 * @game-guild/client - AuthAccountRecovery Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthAccountRecoveryModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Submits one trusted contact's single-use recovery code.
   *
   * Redeems a trusted contact's single-use recovery code after the cooling-off window. Denials are generic and never reveal code or request state.
   */
  async postAuthAccountRecoveryApprove(
    body: Types.IdentityAuthenticationApproveAccountRecoveryInput,
  ): Promise<Result<Types.IdentityAuthenticationAccountRecoveryApprovalResult, ApiError>> {
    const url = '/v1/auth/account-recovery/approve';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationApproveAccountRecoveryInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAccountRecoveryApprovalResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Begins a passkey assertion for recovery approval: an enrolled WebAuthn
   * credential on a surviving device can supply one approval factor.
   *
   * Issues WebAuthn assertion options so an enrolled passkey on a surviving device can approve a live recovery request. Issues no sign-in credentials.
   */
  async postAuthAccountRecoveryApprovePasskeyBegin(
    body: Types.IdentityAuthenticationBeginPasskeyApprovalInput,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnAuthenticationOptionsResult, ApiError>> {
    const url = '/v1/auth/account-recovery/approve-passkey:begin';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationBeginPasskeyApprovalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnAuthenticationOptionsResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Completes a passkey assertion for recovery approval. The assertion is
   * verified cryptographically and never issues sign-in credentials.
   *
   * Verifies the WebAuthn assertion against an enrolled credential and records one approval for the live recovery request. Denials are generic and never reveal request state.
   */
  async postAuthAccountRecoveryApprovePasskeyComplete(
    body: Types.IdentityAuthenticationCompletePasskeyApprovalInput,
  ): Promise<Result<Types.IdentityAuthenticationAccountRecoveryApprovalOutput, ApiError>> {
    const url = '/v1/auth/account-recovery/approve-passkey:complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompletePasskeyApprovalInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAccountRecoveryApprovalOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Cancels the acting user's live recovery request (protective action while the
   * owner still has access).
   *
   * Cancels the acting user's live recovery request and kills any minted reset token.
   */
  async postAuthAccountRecoveryCancel(): Promise<Result<Types.IdentityAuthenticationAccountRecoveryCancelResult, ApiError>> {
    const url = '/v1/auth/account-recovery/cancel';

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAccountRecoveryCancelResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Starts a delegated account recovery for an email address.
   *
   * Opens a delegated account recovery for an email address. Confirmed trusted contacts receive single-use recovery codes after a cooling-off window. Always returns a generic success response to prevent user enumeration.
   */
  async postAuthAccountRecoveryRequest(
    body: Types.IdentityAuthenticationAccountRecoveryRequestInput,
  ): Promise<Result<Types.IdentityAuthenticationAccountRecoveryRequestResult, ApiError>> {
    const url = '/v1/auth/account-recovery/request';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationAccountRecoveryRequestInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAccountRecoveryRequestResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Completes the forced credential reset with the token minted at the approval
   * threshold: sets a new password, terminates all sessions, revokes all refresh
   * tokens and revokes every passkey (mandatory re-enrollment).
   *
   * Consumes the single-use reset token, sets the new password, terminates all sessions, revokes all refresh tokens and revokes every passkey. Denials are generic and never reveal token state.
   */
  async postAuthAccountRecoveryReset(
    body: Types.IdentityAuthenticationCompleteAccountRecoveryResetInput,
  ): Promise<Result<Types.IdentityAuthenticationAccountRecoveryResetResult, ApiError>> {
    const url = '/v1/auth/account-recovery/reset';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteAccountRecoveryResetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationAccountRecoveryResetResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthAccountRecoveryModule(client: ApiClient): AuthAccountRecoveryModule {
  return new AuthAccountRecoveryModule(client);
}
