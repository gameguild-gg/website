/**
 * Tests for Next.js Proxy Helper
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createProxy, createMiddleware } from '../../src/integrations/next/proxy.js';
import type { ResolvedAuthConfig, Session } from '../../src/runtime/auth/types.js';

// Mock dependencies
vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async () => null),
}));

vi.mock('../../src/runtime/auth/session.js', () => ({
  processSession: vi.fn(async (token: string, _config: ResolvedAuthConfig) => {
    if (token === 'valid-token') {
      return {
        session: {
          user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
          expires: new Date(Date.now() + 86400000).toISOString(),
        },
        token: { user: { id: '1' }, accessToken: 'at' },
        updated: false,
      };
    }
    return { session: null, token: null, updated: false };
  }),
}));

function makeConfig(overrides?: Partial<ResolvedAuthConfig>): ResolvedAuthConfig {
  return {
    providers: [],
    callbacks: {
      jwt: async ({ token }) => token,
      session: async ({ session }) => session,
      signIn: async () => true,
      redirect: async ({ url }) => url,
      authorized: async ({ auth }) => !!auth,
    },
    secret: 'test-secret-min-32-chars-long-ok',
    apiUrl: 'http://localhost:5000',
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

describe('createProxy', () => {
  it('should create a proxy wrapper function', () => {
    const config = makeConfig();
    const withAuth = createProxy(config);

    expect(typeof withAuth).toBe('function');
  });

  it('should redirect unauthenticated requests to sign-in', async () => {
    const config = makeConfig();
    const withAuth = createProxy(config);
    const proxyHandler = withAuth();

    const request = new Request('http://localhost:3000/dashboard');
    const response = await proxyHandler(request);

    expect(response.status).toBe(302);
    const location = response.headers.get('Location');
    expect(location).toContain('/sign-in');
    expect(location).toContain('callbackUrl=%2Fdashboard');
  });

  it('should allow authenticated requests to pass through', async () => {
    const config = makeConfig();
    const withAuth = createProxy(config);
    const proxyHandler = withAuth();

    const request = new Request('http://localhost:3000/dashboard', {
      headers: { cookie: '__me.session-token=valid-token' },
    });

    const response = await proxyHandler(request);
    expect(response.status).toBe(200);
  });

  it('should call handler with augmented request when provided', async () => {
    const config = makeConfig();
    const withAuth = createProxy(config);

    const handler = vi.fn(async (req: Request & { auth: Session | null }) => {
      return new Response(JSON.stringify({ user: req.auth?.user }), {
        status: 200,
      });
    });

    const proxyHandler = withAuth(handler);

    const request = new Request('http://localhost:3000/api/data', {
      headers: { cookie: '__me.session-token=valid-token' },
    });

    const response = await proxyHandler(request);
    expect(handler).toHaveBeenCalled();
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.user).toBeDefined();
  });

  it('should attach null session for unauthenticated requests with handler', async () => {
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn: async () => true,
        redirect: async ({ url }) => url,
        authorized: async () => true, // Allow all
      },
    });
    const withAuth = createProxy(config);

    const handler = vi.fn(async (req: Request & { auth: Session | null }) => {
      return new Response(JSON.stringify({ auth: req.auth }), { status: 200 });
    });

    const proxyHandler = withAuth(handler);
    const request = new Request('http://localhost:3000/api/data');
    const response = await proxyHandler(request);

    expect(handler).toHaveBeenCalled();
    const body = await response.json();
    expect(body.auth).toBeNull();
  });

  it('should continue when handler returns void', async () => {
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn: async () => true,
        redirect: async ({ url }) => url,
        authorized: async () => true,
      },
    });
    const withAuth = createProxy(config);

    const handler = vi.fn(async () => {
      // Return nothing
    });

    const proxyHandler = withAuth(handler);
    const request = new Request('http://localhost:3000/');
    const response = await proxyHandler(request);

    expect(response.status).toBe(200);
  });

  it('should use custom sign-in page from config', async () => {
    const config = makeConfig({
      pages: { signIn: '/login' },
    });
    const withAuth = createProxy(config);
    const proxyHandler = withAuth();

    const request = new Request('http://localhost:3000/dashboard');
    const response = await proxyHandler(request);

    expect(response.status).toBe(302);
    const location = response.headers.get('Location');
    expect(location).toContain('/login');
  });

  it('should work without a handler (no-op proxy)', async () => {
    const config = makeConfig({
      callbacks: {
        jwt: async ({ token }) => token,
        session: async ({ session }) => session,
        signIn: async () => true,
        redirect: async ({ url }) => url,
        authorized: async () => true,
      },
    });
    const withAuth = createProxy(config);
    const proxyHandler = withAuth();

    const request = new Request('http://localhost:3000/page');
    const response = await proxyHandler(request);

    expect(response.status).toBe(200);
  });
});

describe('createMiddleware', () => {
  it('should be an alias for createProxy', () => {
    expect(createMiddleware).toBe(createProxy);
  });
});
