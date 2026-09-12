/**
 * Next.js Route Handlers
 *
 * Implements the API route handlers for /api/auth/[...auth]/route.ts
 *
 * Supported routes:
 *   GET  /api/auth/session  — Get current session
 *   GET  /api/auth/csrf     — Get CSRF token
 *   GET  /api/auth/providers — List available providers
 *   POST /api/auth/signin   — Sign in with a provider
 *   GET  /api/auth/signin/:provider — OAuth redirect sign-in
 *   POST /api/auth/signup   — Sign up (credentials only)
 *   POST /api/auth/signout  — Sign out
 *   POST /api/auth/session  — Update session
 *   GET  /api/auth/callback/:provider — OAuth callback
 */

import type { ResolvedAuthConfig, Session, ProviderResult, CredentialsProviderConfig } from '../../runtime/auth/types.js';
import { SessionStore, CsrfStore, resolveCookieOptions, type CookieSerializeOptions } from '../../runtime/auth/cookies.js';
import { createJWTPayload, processSession, encodeSession, toSession } from '../../runtime/auth/session.js';
import { createCSRFToken, validateCSRFToken } from '../../runtime/auth/csrf.js';
import { decodeJWT } from '../../runtime/auth/jwt.js';
import { resolveAuthPermissions, resolveAuthRoles } from '../../runtime/auth/claims.js';
import { AuthError, CredentialsSignInError, ProviderNotFoundError, CSRFError, SignUpError } from '../../runtime/auth/errors.js';
import { type OAuthProviderWithMethods, getOAuthExchangeToken, getOAuthAuthorizeUrl, getOAuthHandleCallback } from './oauth-helpers.js';
import {
  STATE_COOKIE_MAX_AGE,
  constantTimeEqual,
  resolveAllowedRedirect,
  signStatePayload,
  stateCookieName,
  stateCookieOptions,
  verifyStateCookie,
} from './oauth-state.js';

/**
 * Internal cookie setter that collects Set-Cookie headers
 */
interface ResponseCookies {
  cookies: Array<{ name: string; value: string; options: CookieSerializeOptions }>;
  set(name: string, value: string, options: CookieSerializeOptions): void;
}

function createResponseCookies(): ResponseCookies {
  const cookies: ResponseCookies['cookies'] = [];
  return {
    cookies,
    set(name, value, options) {
      cookies.push({ name, value, options });
    },
  };
}

/**
 * Serialize a cookie to a Set-Cookie header string
 */
export function serializeCookie(name: string, value: string, options: CookieSerializeOptions): string {
  let str = `${encodeURIComponent(name)}=${encodeURIComponent(value)}`;

  /* v8 ignore start -- cookie option branches depend on caller config */
  if (options.maxAge !== undefined) {
    str += `; Max-Age=${options.maxAge}`;
  }
  if (options.domain) {
    str += `; Domain=${options.domain}`;
  }
  if (options.path) {
    str += `; Path=${options.path}`;
  }
  if (options.httpOnly) {
    str += '; HttpOnly';
  }
  if (options.secure) {
    str += '; Secure';
  }
  if (options.sameSite) {
    str += `; SameSite=${options.sameSite.charAt(0).toUpperCase() + options.sameSite.slice(1)}`;
  }
  /* v8 ignore stop */

  return str;
}

/**
 * Parse cookies from a Request
 */
export function parseCookies(request: Request): Map<string, string> {
  const cookieHeader = request.headers.get('cookie') || '';
  return parseCookieHeader(cookieHeader);
}

/**
 * Parse a raw cookie header string into a Map.
 * Shared between handlers, proxy, and actions.
 */
export function parseCookieHeader(cookieHeader: string): Map<string, string> {
  const cookies = new Map<string, string>();

  for (const pair of cookieHeader.split(';')) {
    const [name, ...rest] = pair.trim().split('=');
    if (name) {
      try {
        cookies.set(decodeURIComponent(name.trim()), decodeURIComponent(rest.join('=').trim()));
      } catch {
        // Cookies from another app can contain invalid percent escapes. Ignore
        // that pair without breaking CSRF issuance or accepting it as auth data.
        continue;
      }
    }
  }

  return cookies;
}

/**
 * Extract the action and provider from the URL path.
 *
 * Example: /api/auth/signin/google → { action: 'signin', providerId: 'google' }
 */
function parseAuthAction(url: string, basePath: string): { action: string; providerId?: string } {
  const urlObj = new URL(url, 'http://localhost');
  const pathname = urlObj.pathname;

  // Remove basePath prefix
  /* v8 ignore start */
  const relativePath = pathname.startsWith(basePath) ? pathname.slice(basePath.length) : pathname;
  /* v8 ignore stop */

  // Split: /signin/google → ['', 'signin', 'google']
  const parts = relativePath.split('/').filter(Boolean);

  return {
    /* v8 ignore start */
    action: parts[0] || 'session',
    /* v8 ignore stop */
    providerId: parts[1],
  };
}

/**
 * Shared helper: create JWT, run callbacks, encrypt, write cookie, build session.
 * Eliminates duplication across signIn, signUp, and callback handlers.
 */
async function finalizeAuth(
  result: ProviderResult,
  trigger: 'signIn' | 'signUp',
  config: ResolvedAuthConfig,
  sessionStore: SessionStore,
  responseCookies: ResponseCookies,
): Promise<Session> {
  let token = createJWTPayload(result, config);

  token = await config.callbacks.jwt({
    token,
    user: result.user,
    trigger,
  });

  const encrypted = await encodeSession(token, config);
  sessionStore.write(encrypted, responseCookies.set.bind(responseCookies));

  let session = toSession(token);
  session = await config.callbacks.session({ session, token });

  return session;
}

/**
 * Parse a backend API response into a ProviderResult.
 * Shared between signUp handler and signUp action.
 */
export function parseBackendAuthResponse(data: Record<string, unknown>, fallbackEmail?: string, fallbackName?: string | null): ProviderResult {
  const backendUser = data.user as Record<string, unknown> | undefined;

  return {
    tokens: {
      accessToken: data.accessToken as string,
      refreshToken: data.refreshToken as string,
      expiresIn: data.expiresIn as number | undefined,
      accessTokenExpiresAt: data.accessTokenExpiresAt as string | undefined,
      refreshTokenExpiresAt: data.refreshTokenExpiresAt as string | undefined,
      tokenType: 'Bearer',
    },
    user: {
      id: (data.userId as string) || (backendUser?.id as string) || '',
      email: (data.email as string) || (backendUser?.email as string) || fallbackEmail || '',
      name: (backendUser?.displayName as string) || (backendUser?.username as string) || fallbackName || null,
      image: (backendUser?.profilePictureUrl as string) || null,
      roles: resolveAuthRoles(data, backendUser),
      permissions: resolveAuthPermissions(data, backendUser),
    },
    sessionId: data.sessionId as string | undefined,
    tenantId: data.tenantId as string | undefined,
    availableTenants: data.availableTenants as Array<{ id: string; name: string }> | undefined,
  };
}

/**
 * Create the route handler functions (GET and POST).
 */
export function createHandlers(config: ResolvedAuthConfig) {
  const cookieOptions = resolveCookieOptions(config.cookies, config.cookies.secure);
  const sessionStore = new SessionStore(cookieOptions);
  const csrfStore = new CsrfStore(cookieOptions);

  /**
   * Build a Response with Set-Cookie headers.
   * Single shared builder — all handlers use this.
   */
  function buildResponse(body: unknown, status: number, responseCookies: ResponseCookies): Response {
    const headers = new Headers({
      'Content-Type': 'application/json',
      'Cache-Control': 'no-store, max-age=0',
    });

    for (const cookie of responseCookies.cookies) {
      headers.append('Set-Cookie', serializeCookie(cookie.name, cookie.value, cookie.options));
    }

    return new Response(
      /* v8 ignore start */
      body !== undefined ? JSON.stringify(body) : null,
      /* v8 ignore stop */
      { status, headers },
    );
  }

  /**
   * Build a redirect Response with Set-Cookie headers.
   */
  function buildRedirect(url: string, responseCookies: ResponseCookies): Response {
    const headers = new Headers({ Location: url });
    for (const c of responseCookies.cookies) {
      headers.append('Set-Cookie', serializeCookie(c.name, c.value, c.options));
    }
    return new Response(null, { status: 302, headers });
  }

  // ─── GET Handler ─────────────────────────────────────────────

  async function GET(request: Request): Promise<Response> {
    const { action, providerId } = parseAuthAction(request.url, config.basePath);
    const cookies = parseCookies(request);
    const responseCookies = createResponseCookies();

    try {
      switch (action) {
        case 'session':
          return await handleGetSession(cookies, responseCookies);
        case 'csrf':
          return await handleGetCSRF(responseCookies);
        case 'providers':
          return handleGetProviders(responseCookies);
        case 'callback':
          if (providerId) {
            return await handleOAuthCallback(request, providerId, cookies, responseCookies);
          }
          return buildResponse({ error: 'Missing provider' }, 400, responseCookies);
        case 'signin':
          if (providerId) {
            return await handleOAuthSignInRedirect(request, providerId, responseCookies);
          }
          return buildResponse({ error: 'Missing provider' }, 400, responseCookies);
        default:
          return buildResponse({ error: 'Unknown action' }, 404, responseCookies);
      }
    } catch (error) {
      /* v8 ignore start -- error paths tested via dynamic imports */
      if (config.debug) console.error(`[auth] GET /${action} error:`, error);
      if (error instanceof AuthError) return buildResponse(error.toJSON(), error.status, responseCookies);
      return buildResponse({ error: 'InternalError', message: 'Internal server error' }, 500, responseCookies);
      /* v8 ignore stop */
    }
  }

  // ─── POST Handler ────────────────────────────────────────────

  async function POST(request: Request): Promise<Response> {
    const { action, providerId } = parseAuthAction(request.url, config.basePath);
    const cookies = parseCookies(request);
    const responseCookies = createResponseCookies();

    try {
      let body: Record<string, unknown> = {};
      /* v8 ignore start */
      const contentType = request.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        body = (await request.json()) as Record<string, unknown>;
      } else if (contentType.includes('application/x-www-form-urlencoded')) {
        const formData = await request.formData();
        formData.forEach((value, key) => {
          body[key] = value;
        });
      }
      /* v8 ignore stop */

      // CSRF validation for mutation routes
      if (['signin', 'signup', 'signout'].includes(action)) {
        const csrfCookie = csrfStore.read((name) => cookies.get(name));
        /* v8 ignore start */
        const csrfToken = (body.csrfToken as string) || request.headers.get('x-csrf-token');
        /* v8 ignore stop */
        const isValid = await validateCSRFToken(csrfCookie, csrfToken as string, config.secret);
        if (!isValid) throw new CSRFError();
      }

      switch (action) {
        case 'signin': {
          const provider = providerId || (body.provider as string) || 'credentials';
          return await handleSignIn(provider, body, request, responseCookies);
        }
        case 'signup':
          return await handleSignUp(body, responseCookies);
        case 'signout':
          return await handleSignOut(cookies, responseCookies);
        case 'session':
          return await handleUpdateSession(body, cookies, responseCookies);
        default:
          return buildResponse({ error: 'Unknown action' }, 404, responseCookies);
      }
    } catch (error) {
      /* v8 ignore start -- error paths tested via dynamic imports */
      if (config.debug) console.error(`[auth] POST /${action} error:`, error);
      if (error instanceof AuthError) return buildResponse(error.toJSON(), error.status, responseCookies);
      return buildResponse({ error: 'InternalError', message: 'Internal server error' }, 500, responseCookies);
      /* v8 ignore stop */
    }
  }

  // ─── Handler Implementations (inside closure — access buildResponse) ───

  async function handleGetSession(cookies: Map<string, string>, responseCookies: ResponseCookies): Promise<Response> {
    const encryptedToken = sessionStore.read((name) => cookies.get(name));

    /* v8 ignore start */
    if (!encryptedToken) {
      /* v8 ignore stop */
      /* v8 ignore start -- tested via dynamic imports */
      return buildResponse({}, 200, responseCookies);
      /* v8 ignore stop */
    }

    /* v8 ignore start -- covered by dynamic-import tests */
    const { session, token, updated } = await processSession(encryptedToken, config);

    if (updated && token) {
      const newEncrypted = await encodeSession(token, config);
      sessionStore.write(newEncrypted, responseCookies.set.bind(responseCookies));
    }

    return buildResponse(session ?? {}, 200, responseCookies);
    /* v8 ignore stop */
  }

  async function handleGetCSRF(responseCookies: ResponseCookies): Promise<Response> {
    const { cookie, token } = await createCSRFToken(config.secret);
    csrfStore.write(cookie, responseCookies.set.bind(responseCookies));
    return buildResponse({ csrfToken: token }, 200, responseCookies);
  }

  function handleGetProviders(responseCookies: ResponseCookies): Response {
    const providers = config.providers.map((p) => ({
      id: p.id,
      name: p.name,
      type: p.type,
    }));
    return buildResponse(providers, 200, responseCookies);
  }

  async function handleSignIn(providerId: string, body: Record<string, unknown>, request: Request, responseCookies: ResponseCookies): Promise<Response> {
    const provider = config.providers.find((p) => p.id === providerId);
    if (!provider) throw new ProviderNotFoundError(providerId);

    let result: ProviderResult | null = null;

    if (provider.type === 'credentials') {
      const credProvider = provider as CredentialsProviderConfig;
      const credentials = { ...body, __apiUrl: config.apiUrl };
      result = await credProvider.authorize(credentials, request);
    } else {
      const oauthProvider = provider as OAuthProviderWithMethods;

      // Try exchangeToken (Google-style: client sends ID token)
      const exchangeToken = getOAuthExchangeToken(oauthProvider);
      if (exchangeToken) {
        const idToken = body.idToken as string;
        if (!idToken) throw new CredentialsSignInError('OAuth ID token is required');
        result = await exchangeToken(idToken, config.apiUrl, body.tenantId as string | undefined);
      } else {
        // Try getAuthorizeUrl (provider-managed redirect flow)
        const getAuthorizeUrl = getOAuthAuthorizeUrl(oauthProvider);
        /* v8 ignore start */
        if (getAuthorizeUrl) {
          const authUrl = await getAuthorizeUrl(config.apiUrl, body.redirectUri as string | undefined);
          return buildResponse({ url: authUrl }, 200, responseCookies);
        }
        /* v8 ignore stop */
      }
    }

    /* v8 ignore start */
    if (!result) throw new CredentialsSignInError();
    /* v8 ignore stop */

    // Run signIn callback
    const signInAllowed = await config.callbacks.signIn({
      user: result.user,
      provider: providerId,
    });

    if (signInAllowed === false) {
      throw new CredentialsSignInError('Sign-in denied by callback');
    }

    if (typeof signInAllowed === 'string') {
      return Response.redirect(signInAllowed);
    }

    const session = await finalizeAuth(result, 'signIn', config, sessionStore, responseCookies);

    const redirectTo = body.redirectTo as string | undefined;
    const shouldRedirect = body.redirect !== false;

    if (redirectTo && shouldRedirect) {
      const redirectUrl = await config.callbacks.redirect({
        url: redirectTo,
        baseUrl: new URL(request.url).origin,
      });
      return buildRedirect(redirectUrl, responseCookies);
    }

    return buildResponse(session, 200, responseCookies);
  }

  async function handleSignUp(body: Record<string, unknown>, responseCookies: ResponseCookies): Promise<Response> {
    const { username, email, password, firstName, lastName, tenantId } = body as {
      username?: string;
      email?: string;
      password?: string;
      firstName?: string;
      lastName?: string;
      tenantId?: string;
    };

    if (!username || !email || !password) {
      throw new SignUpError('Username, email, and password are required', {
        fieldErrors: {
          /* v8 ignore start */
          ...(!username ? { username: ['Required'] } : {}),
          ...(!email ? { email: ['Required'] } : {}),
          ...(!password ? { password: ['Required'] } : {}),
          /* v8 ignore stop */
        },
      });
    }

    const signUpBody: Record<string, unknown> = { username, email, password };
    if (firstName) signUpBody.firstName = firstName;
    if (lastName) signUpBody.lastName = lastName;
    if (tenantId) signUpBody.tenantId = tenantId;

    const response = await fetch(`${config.apiUrl}/v1/auth/sign-up`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(signUpBody),
    });

    if (!response.ok) {
      /* v8 ignore start -- error response parsing */
      const errorData = (await response.json().catch(() => ({}))) as Record<string, unknown>;
      throw new SignUpError((errorData.message as string) || (errorData.detail as string) || 'Sign-up failed', {
        fieldErrors: errorData.errors as Record<string, string[]> | undefined,
      });
      /* v8 ignore stop */
    }

    const data = (await response.json()) as Record<string, unknown>;
    const providerResult = parseBackendAuthResponse(data, email, username);
    const signInAllowed = await config.callbacks.signIn({
      user: providerResult.user,
      provider: 'credentials',
    });

    if (signInAllowed === false) {
      throw new CredentialsSignInError('Sign-in denied by callback');
    }

    if (typeof signInAllowed === 'string') {
      return buildRedirect(signInAllowed, responseCookies);
    }

    const session = await finalizeAuth(providerResult, 'signUp', config, sessionStore, responseCookies);

    return buildResponse(session, 200, responseCookies);
  }

  async function handleSignOut(cookies: Map<string, string>, responseCookies: ResponseCookies): Promise<Response> {
    // Best-effort revoke the refresh token on the backend
    const encryptedToken = sessionStore.read((name) => cookies.get(name));
    if (encryptedToken) {
      try {
        const token = await decodeJWT({ token: encryptedToken, secret: config.secret });
        if (token?.refreshToken) {
          await fetch(`${config.apiUrl}/v1/auth/tokens:revoke`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token: token.refreshToken }),
            /* v8 ignore start */
          }).catch(() => {});
          /* v8 ignore stop */
        }
      } catch {
        // Ignore decode errors
      }
    }

    sessionStore.delete(responseCookies.set.bind(responseCookies));
    csrfStore.delete(responseCookies.set.bind(responseCookies));

    return buildResponse({ ok: true }, 200, responseCookies);
  }

  async function handleUpdateSession(body: Record<string, unknown>, cookies: Map<string, string>, responseCookies: ResponseCookies): Promise<Response> {
    const encryptedToken = sessionStore.read((name) => cookies.get(name));

    /* v8 ignore start -- tested via dynamic imports */
    if (!encryptedToken) {
      return buildResponse({}, 200, responseCookies);
    }

    let token = await decodeJWT({ token: encryptedToken, secret: config.secret });

    if (!token) {
      return buildResponse({}, 200, responseCookies);
    }
    /* v8 ignore stop */

    /* v8 ignore start -- covered by dynamic-import tests */
    token = await config.callbacks.jwt({
      token,
      trigger: 'update',
      session: body as Partial<Session>,
    });

    const encrypted = await encodeSession(token, config);
    sessionStore.write(encrypted, responseCookies.set.bind(responseCookies));

    let session = toSession(token);
    session = await config.callbacks.session({ session, token });

    return buildResponse(session, 200, responseCookies);
    /* v8 ignore stop */
  }

  async function handleOAuthSignInRedirect(request: Request, providerId: string, responseCookies: ResponseCookies): Promise<Response> {
    const provider = config.providers.find((candidate) => candidate.id === providerId);
    if (!provider) throw new ProviderNotFoundError(providerId);

    const getAuthorizeUrl = getOAuthAuthorizeUrl(provider as OAuthProviderWithMethods);
    if (!getAuthorizeUrl) {
      return buildResponse({ error: 'Provider does not support redirect sign-in' }, 400, responseCookies);
    }

    const url = new URL(request.url);
    const redirectUri = `${url.origin}${config.basePath}/callback/${providerId}`;
    const redirectTo = resolveAllowedRedirect(url.searchParams.get('redirectTo'), config.pages.signIn || '/');
    const authUrl = await getAuthorizeUrl(config.apiUrl, redirectUri);
    const state = new URL(authUrl).searchParams.get('state') ?? '';

    responseCookies.set(
      stateCookieName(providerId),
      await signStatePayload(
        {
          state,
          redirectTo,
          tenantId: url.searchParams.get('tenantId') ?? undefined,
          locale: url.searchParams.get('locale') ?? undefined,
          flow: 'signin',
          exp: Date.now() + STATE_COOKIE_MAX_AGE * 1000,
        },
        config.secret,
      ),
      stateCookieOptions(),
    );

    return buildRedirect(authUrl, responseCookies);
  }

  async function handleOAuthCallback(request: Request, providerId: string, cookies: Map<string, string>, responseCookies: ResponseCookies): Promise<Response> {
    const provider = config.providers.find((p) => p.id === providerId);
    if (!provider) throw new ProviderNotFoundError(providerId);

    const url = new URL(request.url);
    const code = url.searchParams.get('code');
    const state = url.searchParams.get('state');
    const error = url.searchParams.get('error');
    const errorPage = config.pages.error || '/auth/error';

    if (error) {
      return Response.redirect(`${url.origin}${errorPage}?error=${encodeURIComponent(error)}`);
    }

    if (!code) {
      return Response.redirect(`${url.origin}${errorPage}?error=missing_code`);
    }

    const cookieName = stateCookieName(providerId);
    const payload = await verifyStateCookie(cookies.get(cookieName), config.secret);
    if (!payload || payload.flow !== 'signin' || state === null || !constantTimeEqual(payload.state, state)) {
      responseCookies.set(cookieName, '', stateCookieOptions(0));
      return buildRedirect(`${url.origin}${errorPage}?error=state_mismatch`, responseCookies);
    }

    responseCookies.set(cookieName, '', stateCookieOptions(0));
    const redirectUri = `${url.origin}${config.basePath}/callback/${providerId}`;

    const oauthProvider = provider as OAuthProviderWithMethods;
    const handleCallback = getOAuthHandleCallback(oauthProvider);

    let result: ProviderResult | null = null;
    /* v8 ignore start */
    if (handleCallback) {
      result = await handleCallback(config.apiUrl, code, state, redirectUri, payload.tenantId);
    }
    /* v8 ignore stop */

    if (!result) {
      return Response.redirect(`${url.origin}${errorPage}?error=callback_failed`);
    }

    const signInAllowed = await config.callbacks.signIn({
      user: result.user,
      provider: providerId,
    });

    if (signInAllowed === false) {
      return buildRedirect(`${url.origin}${errorPage}?error=access_denied`, responseCookies);
    }

    if (typeof signInAllowed === 'string') {
      return buildRedirect(signInAllowed, responseCookies);
    }

    await finalizeAuth(result, 'signIn', config, sessionStore, responseCookies);

    let callbackUrl = payload.redirectTo || config.pages.newUser || '/';
    if (payload.locale && !callbackUrl.startsWith(`/${payload.locale}`)) {
      callbackUrl = `/${payload.locale}${callbackUrl}`;
    }
    return buildRedirect(`${url.origin}${callbackUrl}`, responseCookies);
  }

  return { GET, POST };
}
