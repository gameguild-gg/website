/**
 * Next.js Server Actions
 *
 * Server-side functions for sign-in, sign-out, sign-up, and session update.
 * These are designed to be called from Server Components and Server Actions.
 *
 * They read/write cookies directly via next/headers and can trigger redirects.
 */

import type { ResolvedAuthConfig, Session, ProviderResult, CredentialsProviderConfig } from '../../runtime/auth/types.js';
import { decodeJWT } from '../../runtime/auth/jwt.js';
import { SessionStore, resolveCookieOptions, type CookieSerializeOptions } from '../../runtime/auth/cookies.js';
import { createJWTPayload, processSession, encodeSession, refreshAccessToken, toSession } from '../../runtime/auth/session.js';
import { CredentialsSignInError, ProviderNotFoundError, SignUpError } from '../../runtime/auth/errors.js';
import { applyResponseCookies, parseBackendAuthResponse } from './handlers.js';
import { type OAuthProviderWithMethods, getOAuthExchangeToken } from './oauth-helpers.js';

/**
 * Cookie adapter — abstracts next/headers cookies() API.
 * This avoids importing next/headers at module level (which would fail
 * in non-Next.js environments).
 */
export interface CookieAdapter {
  get(name: string): { value: string } | undefined;
  set(name: string, value: string, options?: CookieSerializeOptions): void;
  delete(name: string): void;
}

/**
 * Create the server-side auth function.
 *
 * This is the universal `auth()` function that works in:
 * - Server Components (RSC)
 * - Server Actions
 * - Route Handlers
 * - Proxy (formerly Middleware)
 *
 * When called with no arguments, returns the current session.
 * When called with a handler function, returns a proxy-compatible wrapper.
 */
export function createAuthFunction(config: ResolvedAuthConfig) {
  const cookieOptions = resolveCookieOptions(config.cookies, config.cookies.secure);
  const sessionStore = new SessionStore(cookieOptions);

  /**
   * Get the current session from cookies.
   *
   * @param cookieAdapter - Optional cookie adapter. If not provided,
   *                        attempts to use next/headers cookies().
   */
  async function getSession(cookieAdapter?: CookieAdapter): Promise<Session | null> {
    const adapter = cookieAdapter || (await getNextCookies());
    if (!adapter) return null;

    const encryptedToken = sessionStore.read((name) => {
      const cookie = adapter.get(name);
      return cookie?.value;
    });

    if (!encryptedToken) return null;

    const { session, token, updated } = await processSession(encryptedToken, config);

    // If refreshed, update the cookie
    if (updated && token) {
      try {
        const newEncrypted = await encodeSession(token, config);
        const setCookieFn = (name: string, value: string, opts: CookieSerializeOptions) => {
          adapter.set(name, value, opts);
        };
        sessionStore.write(newEncrypted, setCookieFn);
      } catch {
        // In some contexts (like RSC), cookies can't be set
        // The session is still valid, just won't have the updated token
      }
    }

    if (!session) {
      try {
        sessionStore.delete((name) => adapter.delete(name));
      } catch {
        // Server Components can read cookies but cannot mutate them.
      }
    }

    return session;
  }

  /**
   * The overloaded auth function:
   * - auth() → Session | null
   * - auth(handler) → proxy wrapper
   */
  function auth(): Promise<Session | null>;
  function auth(handler: (request: Request & { auth: Session | null }) => Promise<Response> | Response): (request: Request) => Promise<Response>;
  function auth(
    handler?: (request: Request & { auth: Session | null }) => Promise<Response> | Response,
  ): Promise<Session | null> | ((request: Request) => Promise<Response>) {
    if (!handler) {
      return getSession();
    }

    // Return a proxy wrapper
    return async (request: Request): Promise<Response> => {
      // Read session from request cookies
      const { parseCookieHeader } = await import('./handlers.js');
      const cookieHeader = request.headers.get('cookie') || '';
      const cookieMap = parseCookieHeader(cookieHeader);

      const responseCookies: Array<{
        name: string;
        value: string;
        options: CookieSerializeOptions;
      }> = [];
      const adapter: CookieAdapter = {
        get(name) {
          const value = cookieMap.get(name);
          return value !== undefined ? { value } : undefined;
        },
        set(name, value, options = {}) {
          responseCookies.push({ name, value, options });
        },
        delete(name) {
          responseCookies.push({
            name,
            value: '',
            options: {
              httpOnly: cookieOptions.httpOnly, secure: cookieOptions.secure,
              sameSite: cookieOptions.sameSite, path: cookieOptions.path,
              domain: cookieOptions.domain, maxAge: 0,
            },
          });
        },
      };

      const session = await getSession(adapter);

      // Attach session to request
      const augmentedRequest = request as Request & { auth: Session | null };
      augmentedRequest.auth = session;

      const response = await handler(augmentedRequest);
      return applyResponseCookies(response, responseCookies);
    };
  }

  return auth;
}

/**
 * Create the signIn server action.
 */
export function createSignInAction(config: ResolvedAuthConfig) {
  const { sessionStore } = createCookieHelpers(config);

  return async function signIn(
    provider: string = 'credentials',
    options: Record<string, unknown> & {
      redirectTo?: string;
      redirect?: boolean;
    } = {},
  ): Promise<void> {
    const { redirectTo, redirect: shouldRedirect = true, ...credentials } = options;

    const providerConfig = config.providers.find((p) => p.id === provider);
    if (!providerConfig) {
      throw new ProviderNotFoundError(provider);
    }

    let result: ProviderResult | null = null;

    if (providerConfig.type === 'credentials') {
      const credProvider = providerConfig as CredentialsProviderConfig;
      result = await credProvider.authorize({ ...credentials, __apiUrl: config.apiUrl }, undefined);
    } else {
      const oauthProvider = providerConfig as OAuthProviderWithMethods;
      const exchangeToken = getOAuthExchangeToken(oauthProvider);
      if (exchangeToken) {
        result = await exchangeToken(credentials.idToken as string, config.apiUrl, credentials.tenantId as string | undefined);
      }
    }

    if (!result) {
      throw new CredentialsSignInError();
    }

    // signIn callback
    const allowed = await config.callbacks.signIn({
      user: result.user,
      provider,
    });
    if (allowed === false) {
      throw new CredentialsSignInError('Sign-in denied');
    }

    // Finalize auth: create JWT, run callbacks, encrypt, write cookie
    await finalizeServerAction(result, 'signIn', config, sessionStore);

    // Redirect if configured
    if (shouldRedirect && redirectTo) {
      const { redirect } = await import('next/navigation');
      const url = await config.callbacks.redirect({
        url: redirectTo,
        baseUrl: getBaseUrl(),
      });
      redirect(url);
    }
  };
}

/**
 * Create the signUp server action.
 */
export function createSignUpAction(config: ResolvedAuthConfig) {
  const { sessionStore } = createCookieHelpers(config);

  return async function signUp(
    credentials: {
      username: string;
      email: string;
      password: string;
      firstName?: string;
      lastName?: string;
      tenantId?: string;
    },
    options?: { redirectTo?: string; redirect?: boolean },
  ): Promise<void> {
    const { redirectTo, redirect: shouldRedirect = true } = options ?? {};

    const body: Record<string, unknown> = {
      username: credentials.username,
      email: credentials.email,
      password: credentials.password,
    };
    if (credentials.firstName) body.firstName = credentials.firstName;
    if (credentials.lastName) body.lastName = credentials.lastName;
    if (credentials.tenantId) body.tenantId = credentials.tenantId;

    const response = await fetch(`${config.apiUrl}/v1/auth/sign-up`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const errorData = (await response.json().catch(() => ({}))) as Record<string, unknown>;
      throw new SignUpError((errorData.message as string) || (errorData.detail as string) || 'Sign-up failed', {
        fieldErrors: errorData.errors as Record<string, string[]> | undefined,
      });
    }

    const data = (await response.json()) as Record<string, unknown>;
    const result = parseBackendAuthResponse(data, credentials.email, credentials.username);

    await finalizeServerAction(result, 'signUp', config, sessionStore);

    if (shouldRedirect) {
      const { redirect } = await import('next/navigation');
      const url = await config.callbacks.redirect({
        url: redirectTo ?? config.pages.newUser ?? '/',
        baseUrl: getBaseUrl(),
      });
      redirect(url);
    }
  };
}

/**
 * Create the signOut server action.
 */
export function createSignOutAction(config: ResolvedAuthConfig) {
  const { sessionStore } = createCookieHelpers(config);

  return async function signOut(options?: { redirectTo?: string; redirect?: boolean }): Promise<void> {
    const { redirectTo, redirect: shouldRedirect = true } = options ?? {};

    const adapter = await getNextCookies();
    if (adapter) {
      // Try to revoke the server-side token
      const encryptedToken = sessionStore.read((name) => {
        const cookie = adapter.get(name);
        return cookie?.value;
      });

      if (encryptedToken) {
        try {
          const token = await decodeJWT({
            token: encryptedToken,
            secret: config.secret,
          });
          if (token?.refreshToken) {
            await fetch(`${config.apiUrl}/v1/auth/tokens:revoke`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ token: token.refreshToken }),
            }).catch(() => {});
          }
        } catch {
          // Ignore
        }
      }

      // Delete session cookie
      const deleteCookieFn = (name: string, value: string, opts: CookieSerializeOptions) => {
        adapter.set(name, value, opts);
      };
      sessionStore.delete(deleteCookieFn);
    }

    if (shouldRedirect) {
      const { redirect } = await import('next/navigation');
      const url = await config.callbacks.redirect({
        url: redirectTo ?? config.pages.signIn ?? '/',
        baseUrl: getBaseUrl(),
      });
      redirect(url);
    }
  };
}

/**
 * Create the session update server action.
 */
export function createUpdateAction(config: ResolvedAuthConfig) {
  const { sessionStore } = createCookieHelpers(config);

  return async function update(data?: Partial<Session>): Promise<Session | null> {
    const adapter = await getNextCookies();
    if (!adapter) return null;

    const encryptedToken = sessionStore.read((name) => {
      const cookie = adapter.get(name);
      return cookie?.value;
    });

    if (!encryptedToken) return null;

    let token = await decodeJWT({
      token: encryptedToken,
      secret: config.secret,
    });

    if (!token) return null;

    const requestedTenantId = data?.tenantId;
    if (requestedTenantId !== undefined && requestedTenantId !== token.tenantId) {
      token = await refreshAccessToken({ ...token, tenantId: requestedTenantId ?? null }, config);
    }

    // Run JWT callback with update trigger
    token = await config.callbacks.jwt({
      token,
      trigger: 'update',
      session: data,
    });

    // Re-encrypt
    const encrypted = await encodeSession(token, config);
    const setCookieFn = (name: string, value: string, opts: CookieSerializeOptions) => {
      adapter.set(name, value, opts);
    };
    sessionStore.write(encrypted, setCookieFn);

    // Build and return session
    let session = toSession(token);
    session = await config.callbacks.session({ session, token });

    return session;
  };
}

// ─── Helpers ─────────────────────────────────────────────────────

/**
 * Create shared cookie helpers used by all server actions.
 * Eliminates duplicated SessionStore + resolveCookieOptions in each action.
 */
function createCookieHelpers(config: ResolvedAuthConfig) {
  const cookieOptions = resolveCookieOptions(config.cookies, config.cookies.secure);
  const sessionStore = new SessionStore(cookieOptions);

  return {
    sessionStore,
    cookieOptions,
    /**
     * Write encrypted session to next/headers cookie adapter.
     */
    /* v8 ignore start */
    async writeCookieToAdapter(encrypted: string) {
      const adapter = await getNextCookies();
      if (adapter) {
        sessionStore.write(encrypted, (name, value, opts) => {
          adapter.set(name, value, opts);
        });
      }
    },
    /* v8 ignore stop */
  };
}

/**
 * Shared auth finalization for server actions.
 * Creates JWT payload, runs callbacks, encrypts, and writes cookie.
 */
async function finalizeServerAction(
  result: ProviderResult,
  trigger: 'signIn' | 'signUp',
  config: ResolvedAuthConfig,
  sessionStore: SessionStore,
): Promise<void> {
  let token = createJWTPayload(result, config);
  token = await config.callbacks.jwt({
    token,
    user: result.user,
    trigger,
  });

  const encrypted = await encodeSession(token, config);

  const adapter = await getNextCookies();
  /* v8 ignore start */
  if (adapter) {
    // Storage policy: an explicit non-remember sign-in gets a browser-session cookie;
    // remember-me (or the configured default) keeps the durable Max-Age cookie.
    sessionStore.write(encrypted, (name, value, opts) => {
      adapter.set(name, value, opts);
    }, {
      storage: token.rememberMe === false ? 'session' : 'persistent',
    });
  }
  /* v8 ignore stop */
}

/**
 * Dynamically import next/headers cookies().
 * Returns null if not in a Next.js server context.
 */
async function getNextCookies(): Promise<CookieAdapter | null> {
  try {
    /* v8 ignore start -- requires real next/headers */
    const nextHeaders = await import('next/headers');
    const cookieStore = await nextHeaders.cookies();
    return cookieStore as unknown as CookieAdapter;
    /* v8 ignore stop */
  } catch {
    /* v8 ignore start */
    return null;
    /* v8 ignore stop */
  }
}

/**
 * Get the base URL for redirects.
 */
function getBaseUrl(): string {
  /* v8 ignore start */
  if (typeof process !== 'undefined') {
    /* v8 ignore stop */
    return process.env?.NEXTAUTH_URL || process.env?.NEXT_PUBLIC_URL || 'http://localhost:3000';
  }
  /* v8 ignore start -- typeof process is always defined in Node */
  return 'http://localhost:3000';
  /* v8 ignore stop */
}
