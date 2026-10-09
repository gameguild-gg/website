/**
 * GitHub OAuth Provider
 *
 * Bridges GitHub sign-in to the .NET backend.
 * The flow is:
 *   1. Redirect user to .NET `/v1/auth/github:authorize` (which returns the GitHub auth URL)
 *   2. User authenticates with GitHub
 *   3. GitHub redirects back to .NET callback
 *   4. .NET exchanges the code and returns GameGuild tokens
 *
 * @example
 * ```typescript
 * import { GitHubProvider } from '@game-guild/client/auth';
 *
 * GitHubProvider({
 *   clientId: process.env.GITHUB_CLIENT_ID!,
 *   clientSecret: process.env.GITHUB_CLIENT_SECRET!,
 *   apiUrl: process.env.API_URL!,
 * })
 * ```
 */

import type { OAuthProviderConfig, ProviderResult, SessionUser } from '../types.js';
import { OAuthError, parseErrorBody, extractErrorMessage } from '../errors.js';
import { resolveAuthPermissions, resolveAuthRoles } from '../claims.js';
import { assertSafeServiceUrl } from '../../security/safe-remote-url.js';

/**
 * Options for the GitHub provider
 */
export interface GitHubProviderOptions {
  /** GitHub OAuth App client ID */
  clientId: string;
  /** GitHub OAuth App client secret */
  clientSecret: string;
  /**
   * Backend API base URL.
   * If not provided, uses the apiUrl from the main config.
   */
  apiUrl?: string;
  /**
   * Backend endpoint for initiating GitHub auth
   * @default '/v1/auth/github:authorize'
   */
  authorizePath?: string;
  /**
   * Backend endpoint for completing GitHub auth callback
   * @default '/v1/auth/github:callback'
   */
  callbackPath?: string;
}

/**
 * Create a GitHub OAuth provider that bridges to the .NET backend.
 */
export function GitHubProvider(options: GitHubProviderOptions): OAuthProviderConfig & {
  getAuthorizeUrl: (apiUrl: string, redirectUri?: string) => Promise<string>;
  handleCallback: (apiUrl: string, code: string, state?: string) => Promise<ProviderResult>;
} {
  const { authorizePath = '/v1/auth/github:authorize', callbackPath = '/v1/auth/github:callback' } = options;

  return {
    id: 'github',
    name: 'GitHub',
    type: 'oauth',
    clientId: options.clientId,
    clientSecret: options.clientSecret,
    authorization: {
      url: 'https://github.com/login/oauth/authorize',
      params: {
        scope: 'read:user user:email',
      },
    },
    token: { url: 'https://github.com/login/oauth/access_token' },
    userinfo: { url: 'https://api.github.com/user' },

    /**
     * Get the GitHub authorization URL from the .NET backend.
     * The backend manages the OAuth state parameter.
     */
    getAuthorizeUrl: async (apiUrl: string, redirectUri?: string): Promise<string> => {
      const effectiveApiUrl = options.apiUrl || apiUrl;

      const params = new URLSearchParams();
      if (redirectUri) params.set('redirectUri', redirectUri);

      const response = await fetch(assertSafeServiceUrl(`${effectiveApiUrl}${authorizePath}?${params.toString()}`, effectiveApiUrl), {
        method: 'GET',
        redirect: 'error',
      });

      if (!response.ok) {
        const errorData = await parseErrorBody(response);
        throw new OAuthError(extractErrorMessage(errorData, 'Failed to get GitHub authorization URL'));
      }

      const data = (await response.json()) as Record<string, unknown>;
      return data.authUrl as string;
    },

    /**
     * Complete the GitHub OAuth flow by sending the authorization code to the backend.
     */
    handleCallback: async (apiUrl: string, code: string, state?: string): Promise<ProviderResult> => {
      const effectiveApiUrl = options.apiUrl || apiUrl;

      const response = await fetch(assertSafeServiceUrl(`${effectiveApiUrl}${callbackPath}`, effectiveApiUrl), {
        redirect: 'error',
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code, state }),
      });

      if (!response.ok) {
        const errorData = await parseErrorBody(response);
        throw new OAuthError(extractErrorMessage(errorData, 'GitHub sign-in failed'));
      }

      const data = (await response.json()) as Record<string, unknown>;
      const backendUser = data.user as Record<string, unknown> | undefined;

      const user: SessionUser = {
        /* v8 ignore start */
        id: (data.userId as string) || (backendUser?.id as string) || '',
        email: (data.email as string) || (backendUser?.email as string) || '',
        name: (backendUser?.displayName as string) || null,
        image: (backendUser?.profilePictureUrl as string) || null,
        roles: resolveAuthRoles(data, backendUser),
        permissions: resolveAuthPermissions(data, backendUser),
        /* v8 ignore stop */
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
