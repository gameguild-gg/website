/**
 * @game-guild/client - AuthWebauthn Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthWebauthnModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Check if current user has WebAuthn enabled.
   */
  async getAuthWebauthn(): Promise<Result<Types.IdentityAuthenticationWebAuthnStatusOutput, ApiError>> {
    const url = '/v1/auth/webauthn';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnStatusOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Begin WebAuthn authentication (passwordless login).
   */
  async postAuthWebauthnAuthenticationBegin(
    body: Types.IdentityAuthenticationBeginWebAuthnAuthenticationInput,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnAuthenticationOptionsResult, ApiError>> {
    const url = '/v1/auth/webauthn/authentication:begin';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationBeginWebAuthnAuthenticationInputSchema, body, 'request');

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
   * Complete WebAuthn authentication (passwordless login).
   */
  async postAuthWebauthnAuthenticationComplete(
    body: Types.IdentityAuthenticationCompleteWebAuthnAuthenticationInput,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnAuthenticationResult, ApiError>> {
    const url = '/v1/auth/webauthn/authentication:complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteWebAuthnAuthenticationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnAuthenticationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Get all WebAuthn credentials for the current user.
   */
  async getAuthWebauthnCredentialsForGetAuthWebauthnCredentials(): Promise<Result<Types.IdentityAuthenticationWebAuthnCredentialInfo[], ApiError>> {
    const url = '/v1/auth/webauthn/credentials';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    return result as Result<Types.IdentityAuthenticationWebAuthnCredentialInfo[], ApiError>;
  }

  /**
   * Get a single WebAuthn credential by ID.
   */
  async getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialId(
    credentialId: string,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnCredentialInfo, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnCredentialInfoSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Delete a WebAuthn credential. Deletion performs a terminal revocation:
   * the deleted credential can never be restored. Use
   * `:deactivate` for a reversible transition.
   */
  async deleteAuthWebauthnCredentials(credentialId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Update a WebAuthn credential's friendly name.
   */
  async patchAuthWebauthnCredentials(credentialId: string, body: Types.IdentityAuthenticationUpdateCredentialNameInput): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationUpdateCredentialNameInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'PATCH',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Check if a WebAuthn credential exists.
   */
  async headAuthWebauthnCredentials(credentialId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}`;

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Reverse a temporary deactivation of a WebAuthn credential, returning it to
   * active use. Revoked credentials are terminal and are never reactivated.
   */
  async postAuthWebauthnCredentialsActivate(credentialId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}:activate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Temporarily deactivate a WebAuthn credential. Deactivation is reversible via
   * `:activate`; a revoked credential can never be deactivated or restored.
   */
  async postAuthWebauthnCredentialsDeactivate(credentialId: string): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}:deactivate`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Verify a WebAuthn credential is valid and can be used for authentication.
   */
  async postAuthWebauthnCredentialsVerify(credentialId: string): Promise<Result<Types.IdentityAuthenticationWebAuthnCredentialVerifyResult, ApiError>> {
    const url = `/v1/auth/webauthn/credentials/${credentialId}:verify`;

    const result = await this.client.request({
      method: 'POST',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnCredentialVerifyResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Begin WebAuthn credential registration.
   */
  async postAuthWebauthnRegistrationBegin(
    body: Types.IdentityAuthenticationBeginWebAuthnRegistrationInput,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnRegistrationOptionsResult, ApiError>> {
    const url = '/v1/auth/webauthn/registration:begin';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationBeginWebAuthnRegistrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnRegistrationOptionsResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete WebAuthn credential registration.
   */
  async postAuthWebauthnRegistrationComplete(
    body: Types.IdentityAuthenticationCompleteWebAuthnRegistrationInput,
  ): Promise<Result<Types.IdentityAuthenticationWebAuthnRegistrationResult, ApiError>> {
    const url = '/v1/auth/webauthn/registration:complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteWebAuthnRegistrationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWebAuthnRegistrationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthWebauthnModule(client: ApiClient): AuthWebauthnModule {
  return new AuthWebauthnModule(client);
}
