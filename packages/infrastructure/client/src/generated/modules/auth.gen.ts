/**
 * @game-guild/client - Auth Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Initiate Discord OAuth sign-in
   *
   * Initiates the Discord OAuth authorization-code sign-in flow and returns the authorization URL with the CSRF state parameter. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-authorize.
   */
  async postAuthDiscordSignInAuthorize(
    body: Types.IdentityAuthenticationDiscordAuthorizeInput,
  ): Promise<Result<Types.IdentityAuthenticationDiscordSignInOutput, ApiError>> {
    const url = '/v1/auth/discord:sign-in-authorize';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationDiscordAuthorizeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationDiscordSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Handle Discord OAuth callback
   *
   * Exchanges the Discord OAuth authorization code for access and refresh tokens, applying the same account matching and auto-link policy as Google sign-in. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-callback.
   */
  async postAuthDiscordSignInCallback(
    body: Types.IdentityAuthenticationDiscordCallbackRequestDto,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/discord:sign-in-callback';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationDiscordCallbackRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Consume a one-time email sign-in code.
   *
   * Consumes a short-lived single-use six-digit email code and returns access and refresh tokens using the same session issuance path as the magic link.
   */
  async postAuthEmailCodeConsume(body: Types.IdentityAuthenticationConsumeEmailCodeInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/email-code:consume';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationConsumeEmailCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Request a one-time email sign-in code.
   *
   * Generates a short-lived six-digit one-time code and dispatches the email-code notification. Always returns a generic success response to prevent user enumeration.
   */
  async postAuthEmailCodeRequest(
    body: Types.IdentityAuthenticationRequestEmailCodeInput,
  ): Promise<Result<Types.IdentityAuthenticationEmailCodeRequestResult, ApiError>> {
    const url = '/v1/auth/email-code:request';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRequestEmailCodeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationEmailCodeRequestResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Send email verification to user
   *
   * Sends a verification email to the specified email address to confirm ownership.
   */
  async postAuthEmailSendVerification(
    body: Types.IdentityAuthenticationSendEmailVerificationInput,
  ): Promise<Result<Types.IdentityAuthenticationEmailVerificationOutput, ApiError>> {
    const url = '/v1/auth/email:send-verification';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationSendEmailVerificationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationEmailVerificationOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verify email address with token
   *
   * Verifies the user's email address using a token received via email.
   */
  async postAuthEmailVerify(
    body: Types.IdentityAuthenticationVerifyEmailInput,
  ): Promise<Result<Types.IdentityAuthenticationEmailVerificationResult, ApiError>> {
    const url = '/v1/auth/email:verify';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationVerifyEmailInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationEmailVerificationResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List the external logins linked to the current user, newest first.
   *
   * HEAD request per Google REST guidance: safe, metadata-only response with no body. Linked providers and their linked-at timestamps are conveyed in the X-Linked-Providers response header as comma-separated 'provider=iso8601-timestamp' pairs, newest first. The header is omitted when no providers are linked. Granted OAuth scopes (issue #250) are conveyed in the X-Granted-Scopes header as comma-separated 'provider=iso8601-consent-timestamp|consent-version|url-encoded-space-separated-scopes' triples, one entry per link with a recorded consent (consent-version 0 marks a legacy row); the header is omitted when no consents are recorded.
   */
  async headAuthExternalLogins(): Promise<Result<void, ApiError>> {
    const url = '/v1/auth/external-logins';

    const result = await this.client.request({
      method: 'HEAD',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unlink an external provider from the current user.
   *
   * Removes the external login link for the given provider — this is the whole-provider full revocation of every granted scope and the recorded consent. Refused with 400 when it is the user's last sign-in method and no password is set.
   */
  async deleteAuthExternalLogins(provider: string): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/external-logins/${provider}`;

    const result = await this.client.request({
      method: 'DELETE',
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Preview the scopes a provider link flow will request (authorization-time consent screen data).
   *
   * Returns the exact OAuth scopes the provider link flow will request, resolved the same way the authorize step embeds them in the authorization URL. Powers the authorization-time consent surface shown before linking (issue #250).
   */
  async getAuthExternalLoginsLinkPreview(provider: string): Promise<Result<Types.IdentityAuthenticationExternalLoginLinkPreviewOutput, ApiError>> {
    const url = `/v1/auth/external-logins/${provider}/link-preview`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationExternalLoginLinkPreviewOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke individual OAuth scope grants on a linked provider.
   *
   * Revokes individual scope grants on a linked provider without unlinking it; the remaining grant state is returned. Idempotent for scopes that are not currently granted. Revoking every remaining scope leaves the link in place with an empty grant list — whole-provider revocation remains the unlink endpoint (DELETE /external-logins/{provider}), which removes the link entirely.
   */
  async postAuthExternalLoginsScopesRevoke(
    provider: string,
    body: Types.IdentityAuthenticationRevokeExternalLoginScopesInput,
  ): Promise<Result<Types.IdentityAuthenticationRevokeExternalLoginScopesOutput, ApiError>> {
    const url = `/v1/auth/external-logins/${provider}/scopes:revoke`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRevokeExternalLoginScopesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationRevokeExternalLoginScopesOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Start the Discord link flow for the current user.
   *
   * Returns the Discord OAuth authorization URL plus the state parameter to validate at the callback.
   */
  async postAuthExternalLoginsDiscordLinkAuthorize(
    body: Types.IdentityAuthenticationDiscordLinkAuthorizeInput,
  ): Promise<Result<Types.IdentityAuthenticationDiscordLinkAuthorizeOutput, ApiError>> {
    const url = '/v1/auth/external-logins/discord:link-authorize';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationDiscordLinkAuthorizeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationDiscordLinkAuthorizeOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete the Discord link flow for the current user.
   *
   * Exchanges the Discord authorization code for the user profile and links the Discord identity to the authenticated user. Idempotent when already linked to the same user.
   */
  async postAuthExternalLoginsDiscordLinkCallback(body: Types.IdentityAuthenticationDiscordLinkCallbackInput): Promise<Result<void, ApiError>> {
    const url = '/v1/auth/external-logins/discord:link-callback';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationDiscordLinkCallbackInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Link the current user's Google account from a Google ID token.
   *
   * Verifies a Google ID token and links the Google identity to the authenticated user. Idempotent when already linked to the same user.
   */
  async postAuthExternalLoginsGoogle(body: Types.IdentityAuthenticationLinkGoogleAccountInput): Promise<Result<void, ApiError>> {
    const url = '/v1/auth/external-logins/google';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationLinkGoogleAccountInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Initiate GitHub OAuth sign-in
   *
   * Initiates GitHub OAuth authentication flow and returns the authorization URL.
   */
  async getAuthGithubAuthorize(query?: { redirectUri?: string }): Promise<Result<Types.IdentityAuthenticationGitHubSignInOutput, ApiError>> {
    const url = '/v1/auth/github:authorize';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationGitHubSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Handle GitHub OAuth callback
   *
   * Handles the GitHub OAuth callback, exchanging the authorization code for tokens.
   */
  async getAuthGithubCallback(query?: { code?: string; state?: string }): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/github:callback';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Authenticate a user using Google ID Token
   *
   * Authenticates a user using a Google ID Token (for NextAuth.js integration), returning access and refresh tokens. Account-linking counterpart: POST /v1/auth/external-logins/google.
   */
  async postAuthGoogleSignIn(body: Types.IdentityAuthenticationGoogleIdTokenRequestDto): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/google:sign-in';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationGoogleIdTokenRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Consume a passwordless magic sign-in link.
   *
   * Consumes a short-lived one-time magic-link token and returns access and refresh tokens.
   */
  async postAuthMagicLinkConsume(body: Types.IdentityAuthenticationConsumeMagicLinkInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/magic-link:consume';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationConsumeMagicLinkInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Request a passwordless magic sign-in link.
   *
   * Generates a short-lived one-time sign-in token and dispatches the magic-link notification. Always returns a generic success response to prevent user enumeration.
   */
  async postAuthMagicLinkRequest(
    body: Types.IdentityAuthenticationRequestMagicLinkInput,
  ): Promise<Result<Types.IdentityAuthenticationMagicLinkRequestResult, ApiError>> {
    const url = '/v1/auth/magic-link:request';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRequestMagicLinkInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMagicLinkRequestResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete a verified first factor with the account's enrolled or newly provisioned MFA method.
   *
   * Consumes the expiring first-factor challenge and verifies TOTP or a backup code. Identity, tenant, policy and token version come from the server-side challenge. A successful response contains MFA-bound credentials.
   */
  async postAuthMfaSignInComplete(
    body: Types.IdentityAuthenticationCompleteMfaSignInInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/mfa/sign-in/complete';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompleteMfaSignInInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Provision the first TOTP factor using only the limited first-factor challenge.
   *
   * Returns TOTP provisioning data for the server-bound unenrolled account. The expiring challenge grants no session or ordinary credentials. Recovery codes are returned only after TOTP confirmation at the completion endpoint.
   */
  async postAuthMfaSignInEnrollment(
    body: Types.IdentityAuthenticationStartMfaSignInEnrollmentInput,
  ): Promise<Result<Types.IdentityAuthenticationMfaSignInEnrollmentOutput, ApiError>> {
    const url = '/v1/auth/mfa/sign-in/enrollment';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationStartMfaSignInEnrollmentInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationMfaSignInEnrollmentOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Discover federation providers for an email domain
   *
   * Lists the enabled enterprise OIDC federation providers whose configured EmailDomains include the requested address's domain, so the login page can route users before any session exists.
   */
  async getAuthOidcDiscoverProvider(query?: { email?: string }): Promise<Result<Types.IdentityAuthenticationOidcDiscoverProviderOutput, ApiError>> {
    const url = '/v1/auth/oidc:discover-provider';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationOidcDiscoverProviderOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Resolve the provider's front-channel logout URL
   *
   * Returns the provider's discovered end_session_endpoint with the post-logout redirect applied, for front-channel logout forwarding. Local refresh-token revocation is unchanged and remains the caller's responsibility.
   */
  async getAuthOidcEndSessionUrl(
    slug: string,
    query?: { postLogoutRedirectUri?: string },
  ): Promise<Result<Types.IdentityAuthenticationOidcEndSessionUrlOutput, ApiError>> {
    const url = `/v1/auth/oidc/${slug}:end-session-url`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationOidcEndSessionUrlOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initiate OIDC federation provider sign-in
   *
   * Initiates the authorization-code sign-in flow with a configured enterprise OIDC federation provider (Authentication:ExternalProviders:Oidc:<slug>), returning the discovered authorization URL with the CSRF state parameter.
   */
  async postAuthOidcSignInAuthorize(
    slug: string,
    body: Types.IdentityAuthenticationOidcAuthorizeRequestDto,
  ): Promise<Result<Types.IdentityAuthenticationOidcSignInOutput, ApiError>> {
    const url = `/v1/auth/oidc/${slug}:sign-in-authorize`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationOidcAuthorizeRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationOidcSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Handle OIDC federation provider callback
   *
   * Exchanges the OIDC authorization code at the provider's discovered token endpoint, validates the returned ID token (issuer, audience, lifetime, RS256 signature via JWKS), and applies the same account matching and auto-link policy as the social providers. Fail closed when the platform MFA policy is not attested by the provider (amr).
   */
  async postAuthOidcSignInCallback(
    slug: string,
    body: Types.IdentityAuthenticationOidcCallbackRequestDto,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = `/v1/auth/oidc/${slug}:sign-in-callback`;

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationOidcCallbackRequestDtoSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Change password for authenticated user
   *
   * Changes the password for the currently authenticated user.
   */
  async postAuthPasswordChange(
    body: Types.IdentityAuthenticationPasswordChangeInput,
  ): Promise<Result<Types.IdentityAuthenticationPasswordChangeResult, ApiError>> {
    const url = '/v1/auth/password:change';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationPasswordChangeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationPasswordChangeResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete password reset with token
   *
   * Resets the user's password using a token received via email.
   */
  async postAuthPasswordReset(
    body: Types.IdentityAuthenticationCompletePasswordResetInput,
  ): Promise<Result<Types.IdentityAuthenticationPasswordResetResult, ApiError>> {
    const url = '/v1/auth/password:reset';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationCompletePasswordResetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationPasswordResetResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Request password reset
   *
   * Sends a password reset link to the specified email address. Always returns success for security.
   */
  async postAuthPasswordResetRequest(
    body: Types.IdentityAuthenticationRequestPasswordResetInput,
  ): Promise<Result<Types.IdentityAuthenticationPasswordResetRequestResult, ApiError>> {
    const url = '/v1/auth/password:reset-request';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRequestPasswordResetInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationPasswordResetRequestResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Authenticate using email, username or international phone and a password.
   *
   * Resolves one unique account and applies the existing password, risk, tenant and session flow. Phone identifiers use a leading plus and up to 15 digits. Missing, ambiguous or invalid identifiers receive the generic authentication failure.
   */
  async postAuthPolymorphic(body: Types.IdentityAuthenticationPolymorphicSignInInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/polymorphic';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationPolymorphicSignInInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Authenticate a user with email and password
   *
   * Authenticates a user with email and password credentials, returning access and refresh tokens.
   */
  async postAuthSignIn(body: Types.IdentityAuthenticationLocalSignInInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/sign-in';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationLocalSignInInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Register a new user with email and password
   *
   * Creates a new user account with email and password credentials, returning authentication tokens on success.
   */
  async postAuthSignUp(body: Types.IdentityAuthenticationLocalSignUpInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/sign-up';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationLocalSignUpInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Refresh access token using a valid refresh token
   *
   * Exchanges a valid refresh token for a new access token and refresh token pair.
   */
  async postAuthTokensRefresh(body: Types.IdentityAuthenticationRefreshTokenInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/tokens:refresh';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRefreshTokenInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke a refresh token to invalidate it
   *
   * Invalidates a refresh token, preventing it from being used to obtain new access tokens.
   */
  async postAuthTokensRevoke(body: Types.IdentityAuthenticationRevokeRefreshTokenInput): Promise<Result<void, ApiError>> {
    const url = '/v1/auth/tokens:revoke';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationRevokeRefreshTokenInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Verify Web3 wallet signature and authenticate
   *
   * Verifies a Web3 wallet signature against a previously issued challenge and returns authentication tokens.
   */
  async postAuthWeb3Verify(body: Types.IdentityAuthenticationWeb3VerifyInput): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = '/v1/auth/web3:verify';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationWeb3VerifyInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationSignInOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Generate Web3 challenge for wallet authentication
   *
   * Generates a cryptographic challenge that must be signed by the user's wallet to prove ownership.
   */
  async postAuthWeb3Challenge(
    body: Types.IdentityAuthenticationWeb3ChallengeInput,
  ): Promise<Result<Types.IdentityAuthenticationWeb3ChallengeOutput, ApiError>> {
    const url = '/v1/auth/web3/challenge';

    // Validate request body
    const validatedBody = safeParse(Types.IdentityAuthenticationWeb3ChallengeInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: false,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.IdentityAuthenticationWeb3ChallengeOutputSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createAuthModule(client: ApiClient): AuthModule {
  return new AuthModule(client);
}
