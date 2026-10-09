/**
 * Session Management
 *
 * Core session logic: create sessions from provider results, refresh tokens,
 * and convert JWT payloads ↔ Session objects.
 *
 * This is the heart of the auth system — it coordinates between providers,
 * JWT encryption, cookies, and callbacks.
 */

import type { JWTPayload, Session, ProviderResult, ResolvedAuthConfig } from './types.js';
import { encodeJWT, decodeJWT } from './jwt.js';
import { TokenRefreshError } from './errors.js';
import { resolveAuthPermissions, resolveAuthRoles } from './claims.js';

/** Threshold before access token expiry to trigger refresh (30 seconds) */
const REFRESH_THRESHOLD_MS = 30_000;

const inFlightRefreshes = new Map<string, Promise<JWTPayload>>();

/**
 * Create a JWT payload from a provider result (after sign-in/sign-up).
 *
 * @param result - The provider authentication result
 * @param config - Resolved auth configuration
 * @returns The initial JWT payload
 */
export function createJWTPayload(result: ProviderResult, config: ResolvedAuthConfig): JWTPayload {
  const now = Date.now();

  // Calculate access token expiry
  let accessTokenExpires: number;
  if (result.tokens.accessTokenExpiresAt) {
    accessTokenExpires = new Date(result.tokens.accessTokenExpiresAt).getTime();
  } else if (result.tokens.expiresIn) {
    accessTokenExpires = now + result.tokens.expiresIn * 1000;
  } else {
    // Default to 1 hour if not specified
    accessTokenExpires = now + 60 * 60 * 1000;
  }

  // Calculate refresh token expiry
  let refreshTokenExpires: number | undefined;
  if (result.tokens.refreshTokenExpiresAt) {
    refreshTokenExpires = new Date(result.tokens.refreshTokenExpiresAt).getTime();
  }

  const payload: JWTPayload = {
    user: result.user,
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken || '',
    accessTokenExpires,
    refreshTokenExpires,
    sessionId: result.sessionId,
    tenantId: result.tenantId,
    availableTenants: result.availableTenants,
    iat: Math.floor(now / 1000),
    exp: Math.floor(now / 1000) + config.maxAge,
  };

  return payload;
}

/**
 * Convert a JWT payload to a client-safe Session object.
 * Never exposes tokens to the client.
 *
 * @param token - The JWT payload
 * @returns A Session safe for client consumption
 */
export function toSession(token: JWTPayload): Session {
  return {
    user: {
      id: token.user.id,
      email: token.user.email,
      name: token.user.name,
      image: token.user.image,
      roles: token.user.roles,
      permissions: token.user.permissions,
    },
    expires: new Date(token.exp ? token.exp * 1000 : Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    tenantId: token.tenantId,
    availableTenants: token.availableTenants,
  };
}

/**
 * Check if the access token in the JWT payload needs refreshing.
 *
 * @param token - The JWT payload
 * @returns True if the access token is expired or about to expire
 */
export function shouldRefreshToken(token: JWTPayload): boolean {
  if (!token.accessTokenExpires) return false;
  return Date.now() + REFRESH_THRESHOLD_MS >= token.accessTokenExpires;
}

/**
 * Refresh the access token by calling the .NET backend.
 *
 * @param token - The current JWT payload
 * @param config - Resolved auth configuration
 * @returns Updated JWT payload with new tokens
 * @throws TokenRefreshError if refresh fails
 */
export async function refreshAccessToken(token: JWTPayload, config: ResolvedAuthConfig): Promise<JWTPayload> {
  if (!token.refreshToken) {
    throw new TokenRefreshError('No refresh token available');
  }

  const existingRefresh = inFlightRefreshes.get(token.refreshToken);
  if (existingRefresh) {
    return existingRefresh;
  }

  const refreshPromise = executeRefreshAccessToken(token, config);
  inFlightRefreshes.set(token.refreshToken, refreshPromise);

  try {
    return await refreshPromise;
  } finally {
    inFlightRefreshes.delete(token.refreshToken);
  }
}

async function executeRefreshAccessToken(token: JWTPayload, config: ResolvedAuthConfig): Promise<JWTPayload> {
  const refreshUrl = `${config.apiUrl}/v1/auth/tokens:refresh`;

  try {
    const response = await fetch(refreshUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        refreshToken: token.refreshToken,
        ...(token.tenantId ? { tenantId: token.tenantId } : {}),
      }),
    });

    if (!response.ok) {
      throw new TokenRefreshError(`Token refresh failed with status ${response.status}`, undefined, response.status);
    }

    const data = (await response.json()) as Record<string, unknown>;

    // Calculate new expiry
    let accessTokenExpires: number;
    if (data.accessTokenExpiresAt) {
      accessTokenExpires = new Date(data.accessTokenExpiresAt as string).getTime();
    } else if (data.expiresIn) {
      accessTokenExpires = Date.now() + (data.expiresIn as number) * 1000;
    } else {
      accessTokenExpires = Date.now() + 60 * 60 * 1000;
    }

    let refreshTokenExpires: number | undefined;
    if (data.refreshTokenExpiresAt) {
      refreshTokenExpires = new Date(data.refreshTokenExpiresAt as string).getTime();
    }

    const refreshedUser = typeof data.user === 'object' && data.user !== null && !Array.isArray(data.user) ? (data.user as Record<string, unknown>) : undefined;

    const user = refreshedUser
      ? {
          ...token.user,
          id: typeof refreshedUser.id === 'string' ? refreshedUser.id : token.user.id,
          email: typeof refreshedUser.email === 'string' || refreshedUser.email === null ? (refreshedUser.email as string | null) : token.user.email,
          name:
            typeof refreshedUser.displayName === 'string'
              ? refreshedUser.displayName
              : typeof refreshedUser.username === 'string'
                ? refreshedUser.username
                : typeof refreshedUser.name === 'string' || refreshedUser.name === null
                  ? (refreshedUser.name as string | null)
                  : token.user.name,
          image:
            typeof refreshedUser.profilePictureUrl === 'string'
              ? refreshedUser.profilePictureUrl
              : typeof refreshedUser.image === 'string' || refreshedUser.image === null
                ? (refreshedUser.image as string | null)
                : token.user.image,
          roles: resolveAuthRoles(data, refreshedUser),
          permissions: resolveAuthPermissions(data, refreshedUser),
        }
      : {
          ...token.user,
          roles: resolveAuthRoles(data) ?? token.user?.roles,
          permissions: resolveAuthPermissions(data) ?? token.user?.permissions,
        };

    return {
      ...token,
      user,
      accessToken: data.accessToken as string,
      refreshToken: (data.refreshToken as string) || token.refreshToken,
      accessTokenExpires,
      refreshTokenExpires: refreshTokenExpires ?? token.refreshTokenExpires,
      tenantId: typeof data.tenantId === 'string' || data.tenantId === null ? (data.tenantId as string | null) : token.tenantId,
      availableTenants: Array.isArray(data.availableTenants) ? (data.availableTenants as Array<{ id: string; name: string }>) : token.availableTenants,
    };
  } catch (error) {
    if (error instanceof TokenRefreshError) throw error;
    throw new TokenRefreshError('Token refresh failed', error instanceof Error ? error : undefined);
  }
}

/**
 * Process the full session pipeline:
 * 1. Decode the encrypted JWT from cookie
 * 2. Check if access token needs refresh
 * 3. Run the jwt callback
 * 4. Run the session callback
 * 5. Return the session + whether the cookie needs updating
 *
 * @param encryptedToken - The encrypted JWT from the cookie
 * @param config - Resolved auth configuration
 * @returns Session data and whether the JWT was updated (needs re-encryption)
 */
export async function processSession(
  encryptedToken: string,
  config: ResolvedAuthConfig,
): Promise<{
  session: Session | null;
  token: JWTPayload | null;
  updated: boolean;
}> {
  // 1. Decrypt the JWT
  const token = await decodeJWT({
    token: encryptedToken,
    secret: config.secret,
  });

  if (!token) {
    return { session: null, token: null, updated: false };
  }

  // 2. Check if JWT itself has expired (outer envelope)
  if (token.exp && token.exp * 1000 < Date.now()) {
    return { session: null, token: null, updated: false };
  }

  let currentToken = token;
  let tokenUpdated = false;

  // 3. Check if access token needs refresh
  if (shouldRefreshToken(currentToken)) {
    try {
      currentToken = await refreshAccessToken(currentToken, config);
      tokenUpdated = true;
    } catch (error) {
      // A denied refresh cannot keep an authenticated session. A transient
      // failure may retain a still-valid access token until its deadline.
      const refreshDenied = error instanceof TokenRefreshError && (error.responseStatus === 401 || error.responseStatus === 403);
      const accessExpired = currentToken.accessTokenExpires != null && Date.now() >= currentToken.accessTokenExpires;
      if (refreshDenied || accessExpired) {
        /* v8 ignore start */
        if (config.debug) {
          console.warn('[auth] Session cannot be refreshed, requiring authentication');
        }
        /* v8 ignore stop */
        return { session: null, token: null, updated: false };
      }
      // Access token is near-expiry but not yet expired — keep the session
      // alive so the current request can still succeed.
      /* v8 ignore start */
      if (config.debug) {
        console.warn('[auth] Token refresh failed, session may become stale soon');
      }
      /* v8 ignore stop */
    }
  }

  // 4. Run the jwt callback (allows user to modify token)
  /* v8 ignore start */
  if (config.callbacks.jwt) {
    /* v8 ignore stop */
    const callbackResult = await config.callbacks.jwt({ token: currentToken });
    if (callbackResult !== currentToken) {
      currentToken = callbackResult;
      tokenUpdated = true;
    }
  }

  // 5. Build the session
  let session = toSession(currentToken);

  // 6. Run the session callback (allows user to modify exposed session)
  /* v8 ignore start */
  if (config.callbacks.session) {
    /* v8 ignore stop */
    session = await config.callbacks.session({
      session,
      token: currentToken,
    });
  }

  return { session, token: currentToken, updated: tokenUpdated };
}

/**
 * Encode a JWT payload into an encrypted cookie value.
 */
export async function encodeSession(token: JWTPayload, config: ResolvedAuthConfig): Promise<string> {
  return encodeJWT({
    token,
    secret: config.secret,
    maxAge: config.maxAge,
  });
}
