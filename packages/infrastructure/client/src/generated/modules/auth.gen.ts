/**
 * @game-guild/client - Auth Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from "../../runtime/client.js";
import type { Result } from "../../runtime/result/types.js";
import type { ApiError } from "../../runtime/errors/types.js";
import * as Types from "../types.gen.js";
import { safeParse } from "../../runtime/errors/validation.js";

/* eslint-disable @typescript-eslint/no-explicit-any */

export class AuthModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * Register a new user
   *
   * Creates a new user account with email and password credentials, returning authentication tokens on success.
   */
  async postAuthSignUp(
    body: Types.IdentityAuthenticationLocalSignUpInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/sign-up";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationLocalSignUpInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Sign in with email and password
   *
   * Authenticates a user with email and password credentials, returning access and refresh tokens.
   */
  async postAuthSignIn(
    body: Types.IdentityAuthenticationLocalSignInInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/sign-in";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationLocalSignInInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Sign in with Google ID Token
   *
   * Authenticates a user using a Google ID Token (for NextAuth.js integration), returning access and refresh tokens. Account-linking counterpart: POST /v1/auth/external-logins/google.
   */
  async postAuthGoogleSignIn(
    body: Types.IdentityAuthenticationGoogleIdTokenInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/google:sign-in";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationGoogleIdTokenInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Request magic sign-in link
   *
   * Generates a short-lived one-time sign-in token and dispatches the magic-link notification. Always returns a generic success response to prevent user enumeration.
   */
  async postAuthMagicLinkRequest(
    body: Types.IdentityAuthenticationRequestMagicLinkInput,
  ): Promise<
    Result<Types.IdentityAuthenticationMagicLinkRequestResult, ApiError>
  > {
    const url = "/v1/auth/magic-link:request";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationRequestMagicLinkInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationMagicLinkRequestResultSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Consume magic sign-in link
   *
   * Consumes a short-lived one-time magic-link token and returns access and refresh tokens.
   */
  async postAuthMagicLinkConsume(
    body: Types.IdentityAuthenticationConsumeMagicLinkInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/magic-link:consume";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationConsumeMagicLinkInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initiate GitHub OAuth sign-in
   *
   * Initiates GitHub OAuth authentication flow and returns the authorization URL.
   */
  async getAuthGithubAuthorize(query?: {
    redirectUri?: string;
  }): Promise<
    Result<Types.IdentityAuthenticationGitHubSignInOutput, ApiError>
  > {
    const url = "/v1/auth/github:authorize";

    const result = await this.client.request({
      method: "GET",
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationGitHubSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Initiate Discord OAuth sign-in
   *
   * Initiates the Discord OAuth authorization-code sign-in flow and returns the authorization URL with the CSRF state parameter. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-authorize.
   */
  async postAuthDiscordSignInAuthorize(
    body: Types.IdentityAuthenticationDiscordAuthorizeInput,
  ): Promise<
    Result<Types.IdentityAuthenticationDiscordSignInOutput, ApiError>
  > {
    const url = "/v1/auth/discord:sign-in-authorize";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationDiscordAuthorizeInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationDiscordSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Discord OAuth sign-in callback
   *
   * Exchanges the Discord OAuth authorization code for access and refresh tokens, applying the same account matching and auto-link policy as Google sign-in. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-callback.
   */
  async postAuthDiscordSignInCallback(
    body: Types.IdentityAuthenticationDiscordCallbackInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/discord:sign-in-callback";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationDiscordCallbackInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Refresh access token
   *
   * Exchanges a valid refresh token for a new access token and refresh token pair.
   */
  async postAuthTokensRefresh(
    body: Types.IdentityAuthenticationRefreshTokenInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/tokens:refresh";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationRefreshTokenInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Revoke refresh token
   *
   * Invalidates a refresh token, preventing it from being used to obtain new access tokens.
   */
  async postAuthTokensRevoke(
    body: Types.IdentityAuthenticationRevokeRefreshTokenInput,
  ): Promise<Result<void, ApiError>> {
    const url = "/v1/auth/tokens:revoke";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationRevokeRefreshTokenInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Generate Web3 authentication challenge
   *
   * Generates a cryptographic challenge that must be signed by the user's wallet to prove ownership.
   */
  async postAuthWeb3Challenge(
    body: Types.IdentityAuthenticationWeb3ChallengeInput,
  ): Promise<
    Result<Types.IdentityAuthenticationWeb3ChallengeOutput, ApiError>
  > {
    const url = "/v1/auth/web3/challenge";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationWeb3ChallengeInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationWeb3ChallengeOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Send email verification
   *
   * Sends a verification email to the specified email address to confirm ownership.
   */
  async postAuthEmailSendVerification(
    body: Types.IdentityAuthenticationSendEmailVerificationInput,
  ): Promise<
    Result<Types.IdentityAuthenticationEmailVerificationOutput, ApiError>
  > {
    const url = "/v1/auth/email:send-verification";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationSendEmailVerificationInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationEmailVerificationOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verify email with token
   *
   * Verifies the user's email address using a token received via email.
   */
  async postAuthEmailVerify(
    body: Types.IdentityAuthenticationVerifyEmailInput,
  ): Promise<
    Result<Types.IdentityAuthenticationEmailVerificationResult, ApiError>
  > {
    const url = "/v1/auth/email:verify";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationVerifyEmailInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationEmailVerificationResultSchema,
        result.data,
        "response",
      );
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
  ): Promise<
    Result<Types.IdentityAuthenticationPasswordResetRequestResult, ApiError>
  > {
    const url = "/v1/auth/password:reset-request";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationRequestPasswordResetInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationPasswordResetRequestResultSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete password reset
   *
   * Resets the user's password using a token received via email.
   */
  async postAuthPasswordReset(
    body: Types.IdentityAuthenticationCompletePasswordResetInput,
  ): Promise<
    Result<Types.IdentityAuthenticationPasswordResetResult, ApiError>
  > {
    const url = "/v1/auth/password:reset";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationCompletePasswordResetInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationPasswordResetResultSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Change password
   *
   * Changes the password for the currently authenticated user.
   */
  async postAuthPasswordChange(
    body: Types.IdentityAuthenticationPasswordChangeInput,
  ): Promise<
    Result<Types.IdentityAuthenticationPasswordChangeResult, ApiError>
  > {
    const url = "/v1/auth/password:change";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationPasswordChangeInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationPasswordChangeResultSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * GitHub OAuth callback
   *
   * Handles the GitHub OAuth callback, exchanging the authorization code for tokens.
   */
  async getAuthGithubCallback(query?: {
    code?: string;
    state?: string;
  }): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/github:callback";

    const result = await this.client.request({
      method: "GET",
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Verify Web3 signature
   *
   * Verifies a Web3 wallet signature against a previously issued challenge and returns authentication tokens.
   */
  async postAuthWeb3Verify(
    body: Types.IdentityAuthenticationWeb3VerifyInput,
  ): Promise<Result<Types.IdentityAuthenticationSignInOutput, ApiError>> {
    const url = "/v1/auth/web3:verify";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationWeb3VerifyInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationSignInOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List linked external logins
   *
   * HEAD request per Google REST guidance: safe, metadata-only response with no body. Linked providers and their linked-at timestamps are conveyed in the X-Linked-Providers response header as comma-separated 'provider=iso8601-timestamp' pairs, newest first. The header is omitted when no providers are linked.
   */
  async headAuthExternalLogins(): Promise<Result<void, ApiError>> {
    const url = "/v1/auth/external-logins";

    const result = await this.client.request({
      method: "HEAD",
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Link Google account
   *
   * Verifies a Google ID token and links the Google identity to the authenticated user. Idempotent when already linked to the same user.
   */
  async postAuthExternalLoginsGoogle(
    body: Types.IdentityAuthenticationLinkGoogleAccountInput,
  ): Promise<Result<void, ApiError>> {
    const url = "/v1/auth/external-logins/google";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationLinkGoogleAccountInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Start Discord account link
   *
   * Returns the Discord OAuth authorization URL plus the state parameter to validate at the callback.
   */
  async postAuthExternalLoginsDiscordLinkAuthorize(
    body: Types.IdentityAuthenticationDiscordLinkAuthorizeInput,
  ): Promise<
    Result<Types.IdentityAuthenticationDiscordLinkAuthorizeOutput, ApiError>
  > {
    const url = "/v1/auth/external-logins/discord:link-authorize";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationDiscordLinkAuthorizeInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(
        Types.IdentityAuthenticationDiscordLinkAuthorizeOutputSchema,
        result.data,
        "response",
      );
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Complete Discord account link
   *
   * Exchanges the Discord authorization code for the user profile and links the Discord identity to the authenticated user. Idempotent when already linked to the same user.
   */
  async postAuthExternalLoginsDiscordLinkCallback(
    body: Types.IdentityAuthenticationDiscordLinkCallbackInput,
  ): Promise<Result<void, ApiError>> {
    const url = "/v1/auth/external-logins/discord:link-callback";

    // Validate request body
    const validatedBody = safeParse(
      Types.IdentityAuthenticationDiscordLinkCallbackInputSchema,
      body,
      "request",
    );

    const result = await this.client.request({
      method: "POST",
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Unlink external login
   *
   * Removes the external login link for the given provider. Refused with 400 when it is the user's last sign-in method and no password is set.
   */
  async deleteAuthExternalLogins(
    provider: string,
  ): Promise<Result<void, ApiError>> {
    const url = `/v1/auth/external-logins/${provider}`;

    const result = await this.client.request({
      method: "DELETE",
      path: url,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }
}

export function createAuthModule(client: ApiClient): AuthModule {
  return new AuthModule(client);
}
