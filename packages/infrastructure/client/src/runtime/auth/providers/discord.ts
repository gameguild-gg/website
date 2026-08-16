/**
 * Discord OAuth Provider
 *
 * Bridges Discord sign-in to the .NET backend via the
 * authorization-code redirect flow:
 *   1. GET /api/auth/signin/discord → backend `/v1/auth/discord:sign-in-authorize`
 *      returns the Discord auth URL (with state)
 *   2. User authenticates with Discord
 *   3. Discord redirects back to the client callback route
 *   4. Client sends the code to backend `/v1/auth/discord:sign-in-callback`
 *      and receives GameGuild tokens
 *
 * @example
 * ```typescript
 * import { DiscordProvider } from '@game-guild/client/auth';
 *
 * DiscordProvider({
 *   clientId: process.env.DISCORD_CLIENT_ID!,
 *   clientSecret: process.env.DISCORD_CLIENT_SECRET!,
 *   apiUrl: process.env.API_URL!,
 * })
 * ```
 */

import type { OAuthProviderConfig, ProviderResult, SessionUser } from '../types.js';
import { OAuthError } from '../errors.js';

/**
 * Options for the Discord provider
 */
export interface DiscordProviderOptions {
  /** Discord OAuth2 application client ID */
  clientId: string;
  /** Discord OAuth2 application client secret */
  clientSecret: string;
  /**
   * Backend API base URL.
   * If not provided, uses the apiUrl from the main config.
   */
  apiUrl?: string;
  /**
   * Backend endpoint for initiating Discord sign-in
   * @default '/v1/auth/discord:sign-in-authorize'
   */
  authorizePath?: string;
  /**
   * Backend endpoint for completing Discord sign-in callback
   * @default '/v1/auth/discord:sign-in-callback'
   */
  callbackPath?: string;
}

/**
 * Create a Discord OAuth provider that bridges to the .NET backend.
 */
export function DiscordProvider(
  options: DiscordProviderOptions
): OAuthProviderConfig & {
  getAuthorizeUrl: (apiUrl: string, redirectUri?: string) => Promise<string>;
  handleCallback: (
    apiUrl: string,
    code: string,
    state?: string,
    redirectUri?: string,
    tenantId?: string
  ) => Promise<ProviderResult>;
} {
  const {
    authorizePath = '/v1/auth/discord:sign-in-authorize',
    callbackPath = '/v1/auth/discord:sign-in-callback',
  } = options;

  return {
    id: 'discord',
    name: 'Discord',
    type: 'oauth',
    clientId: options.clientId,
    clientSecret: options.clientSecret,
    authorization: {
      url: 'https://discord.com/oauth2/authorize',
      params: {
        scope: 'identify email',
      },
    },
    token: { url: 'https://discord.com/api/oauth2/token' },
    userinfo: { url: 'https://discord.com/api/v10/users/@me' },

    /**
     * Get the Discord authorization URL from the .NET backend.
     * The backend manages the OAuth state parameter (embedded in the authUrl).
     */
    getAuthorizeUrl: async (
      apiUrl: string,
      redirectUri?: string
    ): Promise<string> => {
      const effectiveApiUrl = options.apiUrl || apiUrl;

      const response = await fetch(
        `${effectiveApiUrl}${authorizePath}`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ redirectUri }),
        }
      );

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new OAuthError(
          (errorData as Record<string, unknown>).message as string ||
            'Failed to get Discord authorization URL'
        );
      }

      const data = (await response.json()) as Record<string, unknown>;
      return data.authUrl as string;
    },

    /**
     * Complete the Discord OAuth flow by sending the authorization code to the backend.
     */
    handleCallback: async (
      apiUrl: string,
      code: string,
      state?: string,
      redirectUri?: string,
      tenantId?: string
    ): Promise<ProviderResult> => {
      const effectiveApiUrl = options.apiUrl || apiUrl;

      const body: Record<string, unknown> = { code, state };
      if (redirectUri) body.redirectUri = redirectUri;
      if (tenantId) body.tenantId = tenantId;

      const response = await fetch(
        `${effectiveApiUrl}${callbackPath}`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        }
      );

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new OAuthError(
          (errorData as Record<string, unknown>).message as string ||
            'Discord sign-in failed'
        );
      }

      const data = (await response.json()) as Record<string, unknown>;
      const backendUser = data.user as Record<string, unknown> | undefined;

      const user: SessionUser = {
        /* v8 ignore start */
        id: (data.userId as string) || (backendUser?.id as string) || '',
        email:
          (data.email as string) || (backendUser?.email as string) || '',
        name: (backendUser?.displayName as string) || null,
        image: (backendUser?.profilePictureUrl as string) || null,
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
        availableTenants: data.availableTenants as
          | Array<{ id: string; name: string }>
          | undefined,
      };
    },
  };
}
