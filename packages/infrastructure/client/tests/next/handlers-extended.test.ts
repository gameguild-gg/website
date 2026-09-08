/**
 * Extended Handlers Tests — signIn success, signUp full flow, OAuth, updateSession, form-urlencoded
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createHandlers } from '../../src/integrations/next/handlers.js';
import { signStatePayload, stateCookieName } from '../../src/integrations/next/oauth-state.js';
import type { ResolvedAuthConfig, ProviderResult } from '../../src/runtime/auth/types.js';

async function createOAuthCallbackRequest(providerId: string, secret: string, code = 'abc123', state = 'xyz'): Promise<Request> {
  const cookie = await signStatePayload(
    { state, redirectTo: '/', flow: 'signin', exp: Date.now() + 600_000 },
    secret,
  );
  return new Request(`http://localhost/api/auth/callback/${providerId}?code=${code}&state=${state}`, {
    headers: { cookie: `${stateCookieName(providerId)}=${cookie}` },
  });
}

vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async ({ token }: any) => {
    if (token === 'valid-encrypted-token') {
      return {
        user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        accessTokenExpires: Date.now() + 3600000,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      };
    }
    return null;
  }),
  encodeJWT: vi.fn(async () => 'new-encrypted-token'),
}));

vi.mock('../../src/runtime/auth/csrf.js', () => ({
  createCSRFToken: vi.fn(async () => ({
    cookie: 'csrf-cookie-value',
    token: 'csrf-token-value',
  })),
  validateCSRFToken: vi.fn(async (cookie: string | undefined, token: string | undefined) => {
    return cookie === 'csrf-cookie-value' && token === 'csrf-token-value';
  }),
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
  processSession: vi.fn(async (token: string) => {
    if (token === 'valid-encrypted-token') {
      return {
        session: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          expires: new Date(Date.now() + 86400000).toISOString(),
        },
        token: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          accessToken: 'access-token',
        },
        updated: false,
      };
    }
    return { session: null, token: null, updated: false };
  }),
  encodeSession: vi.fn(async () => 'new-encrypted-token'),
  toSession: vi.fn((token: any) => ({
    user: token.user,
    expires: new Date(Date.now() + 86400000).toISOString(),
  })),
}));

function makeConfig(overrides?: Partial<ResolvedAuthConfig>): ResolvedAuthConfig {
  return {
    providers: [
      {
        id: 'credentials',
        name: 'Credentials',
        type: 'credentials',
        authorize: vi.fn(async (creds: any) => ({
          tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
          user: { id: '1', email: creds.email || 'a@b.com', name: 'Test', image: null },
        })),
      } as any,
    ],
    callbacks: {
      jwt: async ({ token }) => token,
      session: async ({ session }) => session,
      signIn: async () => true,
      redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
      authorized: async ({ auth }) => !!auth,
    },
    secret: 'test-secret-min-32-chars-long-ok',
    apiUrl: 'http://localhost:8080',
    pages: {},
    cookies: {
      name: '__me',
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

function buildRequest(path: string, options: RequestInit & { cookies?: Record<string, string> } = {}): Request {
  const { cookies: cookiesObj, ...init } = options;
  const headers = new Headers(init.headers as HeadersInit | undefined);
  if (cookiesObj) {
    const cookieStr = Object.entries(cookiesObj)
      .map(([k, v]) => `${k}=${v}`)
      .join('; ');
    headers.set('cookie', cookieStr);
  }
  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  return new Request(`http://localhost${path}`, { ...init, headers });
}

describe('Handlers — signIn extended', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(() => {
    if (fetchSpy) fetchSpy.mockRestore();
  });

  it('should sign in with credentials and return session', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        provider: 'credentials',
        email: 'user@example.com',
        password: 'password',
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.user).toBeDefined();
    expect(body.user.email).toBeDefined();
  });

  it('should handle signIn callback returning false (denied)', async () => {
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn: async () => false,
        redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
        authorized: async ({ auth }) => !!auth,
      },
    });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        provider: 'credentials',
        email: 'test@test.com',
        password: 'pw',
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
  });

  it('should handle signIn callback returning a string (redirect URL)', async () => {
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn: async () => 'http://redirect.example.com/path' as any,
        redirect: async ({ url, baseUrl }) => (url.startsWith('/') ? `${baseUrl}${url}` : url),
        authorized: async ({ auth }) => !!auth,
      },
    });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        provider: 'credentials',
        email: 'test@test.com',
        password: 'pw',
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    // signIn returning string → redirect
    expect(response.status).toBe(302);
  });

  it('should redirect with redirectTo after successful signin', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        provider: 'credentials',
        email: 'test@test.com',
        password: 'pw',
        redirectTo: '/dashboard',
        redirect: true,
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBe(302);
    expect(response.headers.get('location')).toContain('/dashboard');
  });

  it('should handle OAuth provider with getAuthorizeUrl', async () => {
    const config = makeConfig({
      providers: [
        {
          id: 'github',
          name: 'GitHub',
          type: 'oauth',
          getAuthorizeUrl: vi.fn(async () => 'https://github.com/login/oauth/authorize?client_id=xxx'),
        } as any,
      ],
    });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin/github', {
      method: 'POST',
      body: JSON.stringify({ csrfToken: 'csrf-token-value' }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.url).toContain('github.com');
  });

  it('should handle OAuth provider with exchangeToken', async () => {
    const config = makeConfig({
      providers: [
        {
          id: 'google',
          name: 'Google',
          type: 'oauth',
          exchangeToken: vi.fn(async () => ({
            tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
            user: { id: '1', email: 'g@g.com', name: 'GUser', image: null },
          })),
        } as any,
      ],
    });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin/google', {
      method: 'POST',
      body: JSON.stringify({ csrfToken: 'csrf-token-value', idToken: 'google-id-token' }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.user).toBeDefined();
  });

  it('should fail signIn with missing OAuth idToken', async () => {
    const config = makeConfig({
      providers: [
        {
          id: 'google',
          name: 'Google',
          type: 'oauth',
          exchangeToken: vi.fn(async () => null),
        } as any,
      ],
    });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin/google', {
      method: 'POST',
      body: JSON.stringify({ csrfToken: 'csrf-token-value' }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
  });
});

describe('Handlers — signUp extended', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(() => {
    if (fetchSpy) fetchSpy.mockRestore();
  });

  it('should sign up with full body and return session', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'at',
          refreshToken: 'rt',
          userId: '1',
        }),
        { status: 200 },
      ),
    );

    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signup', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        username: 'testuser',
        email: 'test@example.com',
        password: 'Password1!',
        firstName: 'Test',
        lastName: 'User',
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.user).toBeDefined();
  });

  it('does not create a session when the signIn callback rejects the new account', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'at',
          refreshToken: 'rt',
          userId: 'renter',
          email: 'renter@example.com',
        }),
        { status: 200 },
      ),
    );
    const signIn = vi.fn(async () => false as const);
    const baseConfig = makeConfig();
    const config = makeConfig({ callbacks: { ...baseConfig.callbacks, signIn } });
    const { POST } = createHandlers(config);

    const response = await POST(
      buildRequest('/api/auth/signup', {
        method: 'POST',
        body: JSON.stringify({
          csrfToken: 'csrf-token-value',
          username: 'renter',
          email: 'renter@example.com',
          password: 'Password1!',
        }),
        cookies: { '__me.csrf-token': 'csrf-cookie-value' },
      }),
    );

    expect(signIn).toHaveBeenCalledWith({
      user: expect.objectContaining({ email: 'renter@example.com' }),
      provider: 'credentials',
    });
    expect(response.status).toBeGreaterThanOrEqual(400);
    expect(response.headers.get('set-cookie') ?? '').not.toContain('__me.session-token=');
  });

  it('should return error for missing required signup fields', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signup', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        email: 'test@example.com',
        // missing username and password
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
    const body = await response.json();
    expect(body.message || body.error).toBeDefined();
  });

  it('should forward API errors from signup', async () => {
    fetchSpy = vi
      .spyOn(global, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ message: 'Email taken', errors: { email: ['Already in use'] } }), { status: 400 }));

    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signup', {
      method: 'POST',
      body: JSON.stringify({
        csrfToken: 'csrf-token-value',
        username: 'testuser',
        email: 'taken@example.com',
        password: 'Password1!',
      }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
  });
});

describe('Handlers — updateSession', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should update session and return updated session', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/session', {
      method: 'POST',
      body: JSON.stringify({ user: { name: 'Updated Name' } }),
      cookies: { '__me.session-token': 'valid-encrypted-token' },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.user).toBeDefined();
  });

  it('should return empty for session update without session cookie', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/session', {
      method: 'POST',
      body: JSON.stringify({ user: { name: 'New' } }),
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    const body = await response.json();
    // No session token → empty response
    expect(body.user).toBeUndefined();
  });
});

describe('Handlers — form-urlencoded POST', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should parse form-urlencoded body', async () => {
    const config = makeConfig();
    const { POST } = createHandlers(config);

    const body = new URLSearchParams();
    body.set('csrfToken', 'csrf-token-value');
    body.set('provider', 'credentials');
    body.set('email', 'test@test.com');
    body.set('password', 'pw');

    const request = new Request('http://localhost/api/auth/signin', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        cookie: '__me.csrf-token=csrf-cookie-value',
      },
      body: body.toString(),
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
  });
});

describe('Handlers — OAuth callback extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should handle successful OAuth callback with code', async () => {
    const config = makeConfig({
      providers: [
        {
          id: 'github',
          name: 'GitHub',
          type: 'oauth',
          handleCallback: vi.fn(async (apiUrl: string, code: string) => ({
            tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
            user: { id: '1', email: 'gh@gh.com', name: 'GH', image: null },
          })),
        } as any,
      ],
    });
    const { GET } = createHandlers(config);

    const request = await createOAuthCallbackRequest('github', config.secret);
    const response = await GET(request);
    expect(response.status).toBe(302);
    expect(config.providers[0].handleCallback).toHaveBeenCalled();
    expect(response.headers.get('location')).toBe('http://localhost/');
  });

  it('should redirect to error page when handleCallback returns null', async () => {
    const config = makeConfig({
      providers: [
        {
          id: 'github',
          name: 'GitHub',
          type: 'oauth',
          handleCallback: vi.fn(async () => null),
        } as any,
      ],
    });
    const { GET } = createHandlers(config);

    const request = await createOAuthCallbackRequest('github', config.secret);
    const response = await GET(request);
    expect(response.status).toBe(302);
    expect(response.headers.get('location')).toContain('callback_failed');
  });

  it('should use custom error page from config', async () => {
    const config = makeConfig({
      pages: { error: '/custom-error' },
      providers: [
        {
          id: 'github',
          name: 'GitHub',
          type: 'oauth',
          handleCallback: vi.fn(),
        } as any,
      ],
    });
    const { GET } = createHandlers(config);

    const request = new Request('http://localhost/api/auth/callback/github?error=access_denied');
    const response = await GET(request);
    expect(response.status).toBe(302);
    expect(response.headers.get('location')).toContain('/custom-error');
  });
});

describe('Handlers — debug logging', () => {
  it('should log GET errors when debug is true', async () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const config = makeConfig({ debug: true });
    const { GET } = createHandlers(config);

    // Callback with unknown provider triggers ProviderNotFoundError
    const request = new Request('http://localhost/api/auth/callback/nonexistent?code=abc');
    const response = await GET(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
    expect(consoleSpy).toHaveBeenCalled();
    consoleSpy.mockRestore();
  });

  it('should log POST errors when debug is true', async () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const config = makeConfig({ debug: true });
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signin', {
      method: 'POST',
      body: JSON.stringify({ csrfToken: 'csrf-token-value', provider: 'nonexistent' }),
      cookies: { '__me.csrf-token': 'csrf-cookie-value' },
    });

    const response = await POST(request);
    expect(response.status).toBeGreaterThanOrEqual(400);
    expect(consoleSpy).toHaveBeenCalled();
    consoleSpy.mockRestore();
  });
});

describe('Handlers — signOut with session token', () => {
  let fetchSpy: any;

  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(() => {
    if (fetchSpy) fetchSpy.mockRestore();
  });

  it('should revoke token on signout when session exists', async () => {
    fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('{}', { status: 200 }));

    const config = makeConfig();
    const { POST } = createHandlers(config);

    const request = buildRequest('/api/auth/signout', {
      method: 'POST',
      body: JSON.stringify({ csrfToken: 'csrf-token-value' }),
      cookies: {
        '__me.csrf-token': 'csrf-cookie-value',
        '__me.session-token': 'valid-encrypted-token',
      },
    });

    const response = await POST(request);
    expect(response.status).toBe(200);
    // Check that token revocation was attempted
    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('tokens:revoke'),
      expect.objectContaining({
        body: JSON.stringify({ token: 'refresh-token' }),
        headers: { 'Content-Type': 'application/json' },
        method: 'POST',
      }),
    );
  });
});
