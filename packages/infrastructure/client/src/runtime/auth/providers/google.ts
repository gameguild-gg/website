/**
 * Google OAuth Provider
 *
 * Bridges Google sign-in to the .NET backend.
 * The flow is:
 *   1. Client gets a Google ID token (via Google Sign-In button or NextAuth Google provider)
 *   2. This provider sends the ID token to .NET `/v1/auth/google:sign-in`
 *   3. .NET validates the Google token and returns GameGuild tokens
 *
 * @example
 * ```typescript
 * import { GoogleProvider } from '@game-guild/client/auth';
 *
 * GoogleProvider({
 *   clientId: process.env.GOOGLE_CLIENT_ID!,
 *   clientSecret: process.env.GOOGLE_CLIENT_SECRET!,
 *   apiUrl: process.env.API_URL!,
 * })
 * ```
 */

import type { OAuthProviderConfig, ProviderResult, SessionUser } from '../types.js';
import { OAuthError, parseErrorBody, extractErrorMessage } from '../errors.js';

/**
 * Options for the Google provider
 */
export interface GoogleProviderOptions {
  /** Google OAuth client ID */
  clientId: string;
  /** Google OAuth client secret */
  clientSecret: string;
  /**
   * Backend API base URL.
   * If not provided, uses the apiUrl from the main config.
   */
  apiUrl?: string;
  /**
   * Backend endpoint path for Google sign-in token exchange
   * @default '/v1/auth/google:sign-in'
   */
  tokenExchangePath?: string;
}

/**
 * Create a Google OAuth provider that bridges to the .NET backend.
 *
 * Unlike a standard OAuth provider, this one takes the Google ID token
 * and sends it to the .NET backend for validation + token issuance.
 */
export function GoogleProvider(
  options: GoogleProviderOptions,
): OAuthProviderConfig & { exchangeToken: (idToken: string, apiUrl: string, tenantId?: string) => Promise<ProviderResult> } {
  const { tokenExchangePath = '/v1/auth/google:sign-in' } = options;

  return {
    id: 'google',
    name: 'Google',
    type: 'oidc',
    clientId: options.clientId,
    clientSecret: options.clientSecret,
    authorization: {
      url: 'https://accounts.google.com/o/oauth2/v2/auth',
      params: {
        scope: 'openid email profile',
        response_type: 'code',
      },
    },
    token: { url: 'https://oauth2.googleapis.com/token' },
    userinfo: { url: 'https://www.googleapis.com/oauth2/v3/userinfo' },

    /**
     * Exchange a Google ID token for .NET backend tokens.
     * Called after the OAuth flow completes.
     */
    exchangeToken: async (idToken: string, apiUrl: string, tenantId?: string): Promise<ProviderResult> => {
      const effectiveApiUrl = options.apiUrl || apiUrl;

      const body: Record<string, unknown> = { idToken };
      if (tenantId) body.tenantId = tenantId;

      const response = await fetch(`${effectiveApiUrl}${tokenExchangePath}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        const errorData = await parseErrorBody(response);
        throw new OAuthError(extractErrorMessage(errorData, 'Google sign-in failed'));
      }

      const data = (await response.json()) as Record<string, unknown>;
      const backendUser = data.user as Record<string, unknown> | undefined;

      const user: SessionUser = {
        /* v8 ignore next */
        id: (data.userId as string) || (backendUser?.id as string) || '',
        email: (data.email as string) || (backendUser?.email as string) || '',
        name: (backendUser?.displayName as string) || null,
        image: (backendUser?.profilePictureUrl as string) || null,
      };

      return {
        tokens: {
          accessToken: data.accessToken as string,
          refreshToken: data.refreshToken as string,
          expiresIn: data.expiresIn as number | undefined,
          accessTokenExpiresAt: data.accessTokenExpiresAt as string | undefined,
          refreshTokenExpiresAt: data.refreshTokenExpiresAt as string | undefined,
          tokenType: 'Bearer',
        },
        user,
        sessionId: data.sessionId as string | undefined,
        tenantId: data.tenantId as string | undefined,
        availableTenants: data.availableTenants as Array<{ id: string; name: string }> | undefined,
      };
    },
  };
}
