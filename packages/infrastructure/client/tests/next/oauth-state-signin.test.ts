/**
 * OAuth redirect sign-in flow tests — GET /api/auth/signin/:provider
 * and the signed state cookie consumed at GET /api/auth/callback/:provider.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createHandlers } from '../../src/integrations/next/handlers.js';
import {
  stateCookieName,
  signStatePayload,
  verifyStateCookie,
  type OAuthStatePayload,
} from '../../src/integrations/next/oauth-state.js';
import type { ResolvedAuthConfig } from '../../src/runtime/auth/types.js';

vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async () => null),
  encodeJWT: vi.fn(async () => 'new-encrypted-token'),
}));

vi.mock('../../src/runtime/auth/csrf.js', () => ({
  createCSRFToken: vi.fn(async () => ({
    cookie: 'csrf-cookie-value',
    token: 'csrf-token-value',
  })),
  validateCSRFToken: vi.fn(async () => true),
}));

vi.mock('../../src/runtime/auth/session.js', () => ({
  createJWTPayload: vi.fn((result: any) => ({
    user: result.user,
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken,
    accessTokenExpires: Date.now() + 3600000,
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 86400,
  })),
  processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
  encodeSession: vi.fn(async () => 'new-encrypted-token'),
  toSession: vi.fn((token: any) => ({
    user: token.user,
    expires: new Date(Date.now() + 86400000).toISOString(),
  })),
}));

const SECRET = 'test-secret-min-32-chars-long-ok';
const AUTH_URL = 'https://discord.com/oauth2/authorize?client_id=x&scope=identify+email&state=st4t3';

function makeDiscordProvider() {
  return {
    id: 'discord',
    name: 'Discord',
    type: 'oauth',
    getAuthorizeUrl: vi.fn(async () => AUTH_URL),
    handleCallback: vi.fn(async () => ({
      tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
      user: { id: '1', email: 'discord@example.com', name: 'Discord User', image: null },
    })),
  } as any;
}

function makeConfig(overrides?: Partial<ResolvedAuthConfig>): ResolvedAuthConfig {
  return {
    providers: [makeDiscordProvider()],
    callbacks: {
      jwt: async ({ token }) => token,
      session: async ({ session }) => session,
      signIn: async () => true,
      redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
      authorized: async ({ auth }) => !!auth,
    },
    secret: SECRET,
    apiUrl: 'http://localhost:5000',
    pages: {},
    cookies: {
      name: '__gg',
      secure: false,
      sameSite: 'lax',
      path: '/',
      maxAge: 2592000,
      httpOnly: true,
    },
    maxAge: 2592000,
    updateAge: 0,
    basePath: '/api/auth',
    debug: false,
    trustHost: false,
    tenantHeader: 'X-Tenant-Id',
    ...overrides,
  };
}

async function makeStateCookie(payload: Partial<OAuthStatePayload> = {}): Promise<string> {
  return signStatePayload(
    {
      state: 'st4t3',
      redirectTo: '/dashboard',
      flow: 'signin',
      exp: Date.now() + 600000,
      ...payload,
    },
    SECRET,
  );
}

function getSetCookies(response: Response): string[] {
  if (typeof response.headers.getSetCookie === 'function') {
    return response.headers.getSetCookie();
  }
  const raw = response.headers.get('set-cookie');
  return raw ? [raw] : [];
}

function findStateCookie(response: Response, providerId = 'discord'): string | undefined {
  return getSetCookies(response).find((c) => c.startsWith(`${stateCookieName(providerId)}=`));
}

describe('GET /api/auth/signin/:provider (OAuth redirect flow)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('redirects to authUrl and sets a signed HttpOnly state cookie', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request(
        'http://localhost:3000/api/auth/signin/discord?redirectTo=%2Fdashboard&tenantId=t1&locale=pt-BR',
      ),
    );

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toBe(AUTH_URL);

    // redirectUri passed to the provider is origin + basePath + /callback/:provider
    expect(config.providers[0].getAuthorizeUrl).toHaveBeenCalledWith(
      'http://localhost:5000',
      'http://localhost:3000/api/auth/callback/discord',
    );

    const setCookie = findStateCookie(response);
    expect(setCookie).toBeDefined();
    expect(setCookie).toContain('HttpOnly');
    expect(setCookie).toContain('SameSite=Lax');
    expect(setCookie).toContain('Max-Age=600');
    expect(setCookie).toContain('Path=/');

    // HMAC round-trip: signature verifies and payload carries the flow data
    const value = setCookie!.split('=').slice(1).join('=').split(';')[0];
    const payload = await verifyStateCookie(value, SECRET);
    expect(payload).not.toBeNull();
    expect(payload!.state).toBe('st4t3'); // extracted from the authUrl
    expect(payload!.redirectTo).toBe('/dashboard');
    expect(payload!.tenantId).toBe('t1');
    expect(payload!.locale).toBe('pt-BR');
    expect(payload!.flow).toBe('signin');
    expect(payload!.exp).toBeGreaterThan(Date.now());
    expect(payload!.exp).toBeLessThanOrEqual(Date.now() + 600000);
  });

  it('rejects protocol-relative redirectTo at stash time (falls back)', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request('http://localhost:3000/api/auth/signin/discord?redirectTo=%2F%2Fevil.com'),
    );

    expect(response.status).toBe(302);
    const setCookie = findStateCookie(response);
    const value = setCookie!.split('=').slice(1).join('=').split(';')[0];
    const payload = await verifyStateCookie(value, SECRET);
    expect(payload!.redirectTo).toBe('/');
  });

  it('rejects absolute redirectTo at stash time (falls back)', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request('http://localhost:3000/api/auth/signin/discord?redirectTo=https%3A%2F%2Fevil.com'),
    );

    const setCookie = findStateCookie(response);
    const value = setCookie!.split('=').slice(1).join('=').split(';')[0];
    const payload = await verifyStateCookie(value, SECRET);
    expect(payload!.redirectTo).toBe('/');
  });

  it('falls back to pages.signIn when redirectTo is invalid', async () => {
    const config = makeConfig({ pages: { signIn: '/sign-in' } });
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request('http://localhost:3000/api/auth/signin/discord?redirectTo=%2F%2Fevil.com'),
    );

    const setCookie = findStateCookie(response);
    const value = setCookie!.split('=').slice(1).join('=').split(';')[0];
    const payload = await verifyStateCookie(value, SECRET);
    expect(payload!.redirectTo).toBe('/sign-in');
  });

  it('returns 400 for a provider without getAuthorizeUrl', async () => {
    const config = makeConfig({
      providers: [{ id: 'google', name: 'Google', type: 'oidc' } as any],
    });
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request('http://localhost:3000/api/auth/signin/google'),
    );

    expect(response.status).toBe(400);
  });

  it('returns 400 when providerId is missing', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(new Request('http://localhost:3000/api/auth/signin'));

    expect(response.status).toBe(400);
  });

  it('returns an error for an unknown provider', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(
      new Request('http://localhost:3000/api/auth/signin/nonexistent'),
    );

    expect(response.status).toBeGreaterThanOrEqual(400);
  });
});

describe('GET /api/auth/callback/:provider (state cookie verification)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function callbackRequest(cookieValue?: string, query = 'code=abc&state=st4t3'): Request {
    const headers: Record<string, string> = {};
    if (cookieValue !== undefined) {
      headers.cookie = `${stateCookieName('discord')}=${cookieValue}`;
    }
    return new Request(`http://localhost:3000/api/auth/callback/discord?${query}`, { headers });
  }

  it('rejects a missing state cookie with state_mismatch', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(callbackRequest(undefined));

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toBe(
      'http://localhost:3000/auth/error?error=state_mismatch',
    );
    expect(findStateCookie(response)).toContain('Max-Age=0');
    expect(config.providers[0].handleCallback).not.toHaveBeenCalled();
  });

  it('rejects a tampered cookie payload (HMAC failure)', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const valid = await makeStateCookie();
    const dot = valid.indexOf('.');
    const tampered = (valid.slice(0, dot)[0] === 'A' ? 'B' : 'A') + valid.slice(1);

    const response = await GET(callbackRequest(tampered));

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toContain('error=state_mismatch');
    expect(findStateCookie(response)).toContain('Max-Age=0');
    expect(config.providers[0].handleCallback).not.toHaveBeenCalled();
  });

  it('rejects a tampered HMAC segment', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const valid = await makeStateCookie();
    const tampered = valid.slice(0, -2) + 'zz'; // flip hex digest tail

    const response = await GET(callbackRequest(tampered));

    expect(response.headers.get('Location')).toContain('error=state_mismatch');
  });

  it('rejects an expired cookie', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const expired = await makeStateCookie({ exp: Date.now() - 1000 });

    const response = await GET(callbackRequest(expired));

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toContain('error=state_mismatch');
    expect(findStateCookie(response)).toContain('Max-Age=0');
  });

  it('rejects a cookie with the wrong flow', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const wrongFlow = await makeStateCookie({ flow: 'link' as OAuthStatePayload['flow'] });

    const response = await GET(callbackRequest(wrongFlow));

    expect(response.headers.get('Location')).toContain('error=state_mismatch');
    expect(config.providers[0].handleCallback).not.toHaveBeenCalled();
  });

  it('rejects when query state does not match cookie state', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const response = await GET(callbackRequest(await makeStateCookie(), 'code=abc&state=evil'));

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toContain('error=state_mismatch');
    expect(findStateCookie(response)).toContain('Max-Age=0');
    expect(config.providers[0].handleCallback).not.toHaveBeenCalled();
  });

  it('accepts a valid cookie: calls handleCallback, consumes cookie, redirects locale-prefixed', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const cookieValue = await makeStateCookie({ tenantId: 't1', locale: 'pt-BR' });
    const response = await GET(callbackRequest(cookieValue));

    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toBe('http://localhost:3000/pt-BR/dashboard');

    expect(config.providers[0].handleCallback).toHaveBeenCalledWith(
      'http://localhost:5000',
      'abc',
      'st4t3',
      'http://localhost:3000/api/auth/callback/discord',
      't1',
    );

    // state cookie consumed
    expect(findStateCookie(response)).toContain('Max-Age=0');

    // finalizeAuth ran → session cookie written
    const allCookies = getSetCookies(response);
    expect(allCookies.some((c) => c.startsWith('__gg.session-token='))).toBe(true);
  });

  it('does not create a session when the signIn callback rejects an OAuth identity', async () => {
    const signIn = vi.fn(async () => false as const);
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn,
        redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
        authorized: async ({ auth }) => !!auth,
      },
    });
    const { GET } = createHandlers(config);

    const response = await GET(callbackRequest(await makeStateCookie()));

    expect(signIn).toHaveBeenCalledWith({
      user: expect.objectContaining({ email: 'discord@example.com' }),
      provider: 'discord',
    });
    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toContain('error=access_denied');
    expect(getSetCookies(response).some((cookie) => cookie.startsWith('__gg.session-token='))).toBe(false);
  });

  it('does not double-prefix an already locale-prefixed redirectTo', async () => {
    const config = makeConfig();
    const { GET } = createHandlers(config);

    const cookieValue = await makeStateCookie({ redirectTo: '/pt-BR/dashboard', locale: 'pt-BR' });
    const response = await GET(callbackRequest(cookieValue));

    expect(response.headers.get('Location')).toBe('http://localhost:3000/pt-BR/dashboard');
  });

  it('uses pages.error from config for state_mismatch redirects', async () => {
    const config = makeConfig({ pages: { error: '/auth-error' } });
    const { GET } = createHandlers(config);

    const response = await GET(callbackRequest(undefined));

    expect(response.headers.get('Location')).toContain('/auth-error?error=state_mismatch');
  });
});
