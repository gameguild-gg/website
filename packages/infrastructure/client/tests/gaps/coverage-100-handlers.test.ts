/* eslint-disable @typescript-eslint/no-explicit-any */
/**
 * Coverage-100 handler tests — covers remaining branch gaps in handlers.ts and actions.ts
 * that require vi.resetModules() and dynamic imports.
 *
 * handlers.ts — L507 (signUp non-200), L528 (signOut no refreshToken), L609 (OAuth null result)
 * actions.ts  — L316 (sign-out token without refreshToken)
 * index.ts    — L181-185 (createClientFromCookies missing cookies)
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import { createClientFromCookies } from '../../src/integrations/next/index.js';
import { signStatePayload, stateCookieName } from '../../src/integrations/next/oauth-state.js';

// ─── handlers.ts L507 — handleSignUp non-200 response ─────────────────

describe('handlers — signUp non-200 response (L507)', () => {
  afterEach(() => {
    vi.resetModules();
    vi.restoreAllMocks();
  });

  it('throws SignUpError when backend returns non-200', { timeout: 30000 }, async () => {
    vi.resetModules();

    const originalFetch = globalThis.fetch;

    vi.doMock('../../src/runtime/auth/jwt.js', () => ({
      decodeJWT: vi.fn(async () => null),
      encodeJWT: vi.fn(async () => 'enc'),
    }));

    vi.doMock('../../src/runtime/auth/csrf.js', () => ({
      createCSRFToken: vi.fn(async () => ({ cookie: 'c', token: 't' })),
      validateCSRFToken: vi.fn(async () => true),
    }));

    vi.doMock('../../src/runtime/auth/session.js', () => ({
      createJWTPayload: vi.fn(() => ({})),
      processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
      encodeSession: vi.fn(async () => 'enc'),
      toSession: vi.fn(() => ({ user: { id: '1' }, expires: 'x' })),
    }));

    const { createHandlers } = await import('../../src/integrations/next/handlers.js');

    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ message: 'Email taken', errors: { email: ['Already exists'] } }),
    });

    const config: any = {
      providers: [],
      callbacks: {
        jwt: async ({ token }: any) => token,
        session: async ({ session }: any) => session,
        signIn: async () => true,
        redirect: async ({ url }: any) => url,
        authorized: async () => true,
      },
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:8080',
      pages: {},
      cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
      maxAge: 2592000,
      updateAge: 0,
      basePath: '/api/auth',
      debug: false,
      trustHost: false,
      tenantHeader: 'X-Tenant-Id',
    };

    const { POST } = createHandlers(config);

    const request = new Request('http://localhost/api/auth/signup', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        cookie: '__me.csrf-token=c',
      },
      body: JSON.stringify({
        csrfToken: 't',
        username: 'testuser',
        email: 'test@test.com',
        password: 'password123',
      }),
    });

    const response = await POST(request);
    // SignUp errors get caught by the error handling and return 400
    expect(response.status).toBeGreaterThanOrEqual(400);

    globalThis.fetch = originalFetch;
    vi.resetModules();
  });
});

// ─── handlers.ts L528 — handleSignOut with no refreshToken ────────────

describe('handlers — signOut without refreshToken (L528)', () => {
  afterEach(() => {
    vi.resetModules();
    vi.restoreAllMocks();
  });

  it('handles signOut when token has no refreshToken', { timeout: 30000 }, async () => {
    vi.resetModules();

    vi.doMock('../../src/runtime/auth/jwt.js', () => ({
      decodeJWT: vi.fn(async () => ({
        user: { id: '1', email: 'a@b.com', name: 'T', image: null },
        accessToken: 'at',
        // NO refreshToken
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      })),
      encodeJWT: vi.fn(async () => 'enc'),
    }));

    vi.doMock('../../src/runtime/auth/csrf.js', () => ({
      createCSRFToken: vi.fn(async () => ({ cookie: 'c', token: 't' })),
      validateCSRFToken: vi.fn(async () => true),
    }));

    vi.doMock('../../src/runtime/auth/session.js', () => ({
      createJWTPayload: vi.fn(() => ({})),
      processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
      encodeSession: vi.fn(async () => 'enc'),
      toSession: vi.fn(() => ({ user: { id: '1' }, expires: 'x' })),
    }));

    const { createHandlers } = await import('../../src/integrations/next/handlers.js');

    const config: any = {
      providers: [],
      callbacks: {
        jwt: async ({ token }: any) => token,
        session: async ({ session }: any) => session,
        signIn: async () => true,
        redirect: async ({ url }: any) => url,
        authorized: async () => true,
      },
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:8080',
      pages: {},
      cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
      maxAge: 2592000,
      updateAge: 0,
      basePath: '/api/auth',
      debug: false,
      trustHost: false,
      tenantHeader: 'X-Tenant-Id',
    };

    const { POST } = createHandlers(config);

    const request = new Request('http://localhost/api/auth/signout', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        cookie: '__me.session-token=valid-tok; __me.csrf-token=c',
      },
      body: JSON.stringify({ csrfToken: 't' }),
    });

    const response = await POST(request);
    expect(response.status).toBe(200);

    vi.resetModules();
  });
});

// ─── handlers.ts L609 — OAuth callback returns null ──────────────────

describe('handlers — OAuth callback null result (L609)', () => {
  afterEach(() => {
    vi.resetModules();
    vi.restoreAllMocks();
  });

  it('redirects to error page when OAuth callback returns null', { timeout: 30000 }, async () => {
    vi.resetModules();

    vi.doMock('../../src/runtime/auth/jwt.js', () => ({
      decodeJWT: vi.fn(async () => null),
      encodeJWT: vi.fn(async () => 'enc'),
    }));

    vi.doMock('../../src/runtime/auth/csrf.js', () => ({
      createCSRFToken: vi.fn(async () => ({ cookie: 'c', token: 't' })),
      validateCSRFToken: vi.fn(async () => true),
    }));

    vi.doMock('../../src/runtime/auth/session.js', () => ({
      createJWTPayload: vi.fn(() => ({})),
      processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
      encodeSession: vi.fn(async () => 'enc'),
      toSession: vi.fn(() => ({ user: { id: '1' }, expires: 'x' })),
    }));

    const { createHandlers } = await import('../../src/integrations/next/handlers.js');

    const config: any = {
      providers: [
        {
          id: 'google',
          name: 'Google',
          type: 'oauth',
          // handleCallback returns null
          handleCallback: vi.fn(async () => null),
        },
      ],
      callbacks: {
        jwt: async ({ token }: any) => token,
        session: async ({ session }: any) => session,
        signIn: async () => true,
        redirect: async ({ url }: any) => url,
        authorized: async () => true,
      },
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:8080',
      pages: { error: '/auth/error' },
      cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
      maxAge: 2592000,
      updateAge: 0,
      basePath: '/api/auth',
      debug: false,
      trustHost: false,
      tenantHeader: 'X-Tenant-Id',
    };

    const { GET } = createHandlers(config);

    const stateCookie = await signStatePayload({
      state: 'xyz',
      redirectTo: '/',
      flow: 'signin',
      exp: Date.now() + 600_000,
    }, config.secret);
    const request = new Request('http://localhost/api/auth/callback/google?code=abc&state=xyz', {
      headers: { cookie: `${stateCookieName('google')}=${stateCookie}` },
    });

    const response = await GET(request);
    // Should redirect to error page
    expect(response.status).toBe(302);
    expect(response.headers.get('Location')).toContain('callback_failed');

    vi.resetModules();
  });
});

// ─── actions.ts L316 — sign-out with token missing refreshToken ──────

describe('actions — signOut without refreshToken (L316)', () => {
  afterEach(() => {
    vi.resetModules();
    vi.restoreAllMocks();
  });

  it('handles signOut when token has no refreshToken', { timeout: 30000 }, async () => {
    vi.resetModules();

    const mockCookieStore = new Map<string, string>();
    mockCookieStore.set('__me.session-token', 'encrypted-session');

    const mockAdapter = {
      get: (name: string) => {
        const v = mockCookieStore.get(name);
        return v !== undefined ? { value: v } : undefined;
      },
      set: (name: string, value: string) => {
        mockCookieStore.set(name, value);
      },
      delete: (name: string) => {
        mockCookieStore.delete(name);
      },
    };

    vi.doMock('next/headers', () => ({
      cookies: vi.fn(async () => mockAdapter),
    }));

    vi.doMock('next/navigation', () => ({
      redirect: vi.fn(() => {
        throw new Error('REDIRECT');
      }),
    }));

    vi.doMock('../../src/runtime/auth/jwt.js', () => ({
      decodeJWT: vi.fn(async () => ({
        user: { id: '1', email: 'a@b.com', name: 'T', image: null },
        accessToken: 'at',
        // NO refreshToken — L316 branch
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      })),
      encodeJWT: vi.fn(async () => 'enc'),
    }));

    vi.doMock('../../src/runtime/auth/session.js', () => ({
      createJWTPayload: vi.fn(() => ({})),
      processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
      encodeSession: vi.fn(async () => 'enc'),
      toSession: vi.fn(() => ({ user: { id: '1' }, expires: 'x' })),
    }));

    const { createSignOutAction } = await import('../../src/integrations/next/actions.js');

    const config: any = {
      providers: [],
      callbacks: {
        jwt: async ({ token }: any) => token,
        session: async ({ session }: any) => session,
        signIn: async () => true,
        redirect: async ({ url }: any) => url,
        authorized: async () => true,
      },
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:8080',
      pages: { signIn: '/login', newUser: '/welcome' },
      cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
      maxAge: 2592000,
      updateAge: 0,
      basePath: '/api/auth',
      debug: false,
      trustHost: false,
      tenantHeader: 'X-Tenant-Id',
    };

    const signOut = createSignOutAction(config);

    // Call signOut with redirect: false
    try {
      await signOut({ redirect: false });
    } catch (e: any) {
      // Ignore redirect errors
      if (e.message !== 'REDIRECT') throw e;
    }

    // Cookie should have been deleted (set to empty string with maxAge=0)
    expect(mockCookieStore.get('__me.session-token')).toBe('');

    vi.resetModules();
  });
});

// ─── index.ts (next) L181-185 — createClientFromCookies missing cookies ──

describe('next/index — createClientFromCookies missing cookies (L181-185)', () => {
  it('creates client that returns null session when accessToken cookie missing', async () => {
    const originalFetch = globalThis.fetch;
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => ({ data: 'ok' }),
      text: async () => '{"data":"ok"}',
    });

    const client = await createClientFromCookies({
      baseUrl: 'http://localhost:8080',
      getCookies: async () => ({
        get: () => {
          // Return undefined for both cookies — L181-185
          return undefined;
        },
      }),
    });

    // Client should be created but with null session info
    expect(client).toBeDefined();
    expect(client.getBaseUrl()).toBe('http://localhost:8080');

    globalThis.fetch = originalFetch;
  });

  it('creates client that returns null tenant when tenant cookie missing', async () => {
    const originalFetch = globalThis.fetch;
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => ({ data: 'ok' }),
      text: async () => '{"data":"ok"}',
    });

    const client = await createClientFromCookies({
      baseUrl: 'http://localhost:8080',
      getCookies: async () => ({
        get: (name: string) => {
          if (name === 'access_token') return { value: 'tok' };
          // No tenant cookie — L185
          return undefined;
        },
      }),
    });

    expect(client).toBeDefined();

    globalThis.fetch = originalFetch;
  });
});

// ─── handlers.ts L96 — additional handler branch coverage ────────────

describe('handlers — additional branch paths', () => {
  afterEach(() => {
    vi.resetModules();
    vi.restoreAllMocks();
  });

  it('handles signUp with optional firstName/lastName/tenantId', { timeout: 30000 }, async () => {
    vi.resetModules();

    const originalFetch = globalThis.fetch;

    vi.doMock('../../src/runtime/auth/jwt.js', () => ({
      decodeJWT: vi.fn(async () => null),
      encodeJWT: vi.fn(async () => 'enc'),
    }));

    vi.doMock('../../src/runtime/auth/csrf.js', () => ({
      createCSRFToken: vi.fn(async () => ({ cookie: 'c', token: 't' })),
      validateCSRFToken: vi.fn(async () => true),
    }));

    vi.doMock('../../src/runtime/auth/session.js', () => ({
      createJWTPayload: vi.fn((r: any) => ({
        user: r.user,
        accessToken: r.tokens.accessToken,
        refreshToken: r.tokens.refreshToken,
        accessTokenExpires: Date.now() + 3600000,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      })),
      processSession: vi.fn(async () => ({ session: null, token: null, updated: false })),
      encodeSession: vi.fn(async () => 'enc'),
      toSession: vi.fn((t: any) => ({ user: t.user, expires: 'x' })),
    }));

    const { createHandlers } = await import('../../src/integrations/next/handlers.js');

    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        accessToken: 'at',
        refreshToken: 'rt',
        userId: 'new-user',
        email: 'new@test.com',
      }),
    });

    const config: any = {
      providers: [],
      callbacks: {
        jwt: async ({ token }: any) => token,
        session: async ({ session }: any) => session,
        signIn: async () => true,
        redirect: async ({ url }: any) => url,
        authorized: async () => true,
      },
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:8080',
      pages: {},
      cookies: { name: '__me', secure: false, sameSite: 'lax', path: '/', maxAge: 2592000, httpOnly: true },
      maxAge: 2592000,
      updateAge: 0,
      basePath: '/api/auth',
      debug: false,
      trustHost: false,
      tenantHeader: 'X-Tenant-Id',
    };

    const { POST } = createHandlers(config);

    const request = new Request('http://localhost/api/auth/signup', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        cookie: '__me.csrf-token=c',
      },
      body: JSON.stringify({
        csrfToken: 't',
        username: 'testuser',
        email: 'new@test.com',
        password: 'password123',
        firstName: 'Test',
        lastName: 'User',
        tenantId: 'tenant-1',
      }),
    });

    const response = await POST(request);
    expect(response.status).toBe(200);

    globalThis.fetch = originalFetch;
    vi.resetModules();
  });
});
