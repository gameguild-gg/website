/**
 * Tests for Next.js Route Handlers
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { parseCookieHeader, parseCookies, parseBackendAuthResponse, createHandlers } from '../../src/integrations/next/handlers.js';
import type { CredentialsProviderConfig, JWTPayload, ResolvedAuthConfig, ProviderResult } from '../../src/runtime/auth/types.js';
import type { OAuthProviderWithMethods } from '../../src/integrations/next/oauth-helpers.js';

type DecodeJWTParams = Parameters<typeof import('../../src/runtime/auth/jwt.js').decodeJWT>[0];
type OAuthCallbackFixture = Omit<OAuthProviderWithMethods, 'clientId' | 'clientSecret'>;

function legacyOAuthFixture(provider: OAuthCallbackFixture): OAuthProviderWithMethods {
  // These early-error cases deliberately omit credentials. No exchange executes.
  return provider as OAuthProviderWithMethods;
}

function makeUnsignedJwt(payload: Record<string, unknown>): string {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString('base64url');
  return `${encode({ alg: 'none', typ: 'JWT' })}.${encode(payload)}.`;
}

// Mock the auth modules
vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async ({ token }: DecodeJWTParams) => {
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
    if (token === 'expired-encrypted-token') {
      return {
        user: { id: '1', email: 'test@example.com', name: 'Test', image: null },
        accessToken: 'access-token',
        refreshToken: 'refresh-token',
        accessTokenExpires: Date.now() - 60000,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) - 100,
      };
    }
    return null;
  }),
  encodeJWT: vi.fn(async () => 'new-encrypted-token'),
}));

vi.mock('../../src/runtime/auth/csrf.js', () => ({
  createCSRFToken: vi.fn(async (secret: string) => ({
    cookie: 'csrf-cookie-value',
    token: 'csrf-token-value',
  })),
  validateCSRFToken: vi.fn(async (cookie: string | undefined, token: string | undefined, secret: string) => {
    return cookie === 'csrf-cookie-value' && token === 'csrf-token-value';
  }),
}));

vi.mock('../../src/runtime/auth/session.js', () => ({
  createJWTPayload: vi.fn((result: ProviderResult) => ({
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
  toSession: vi.fn((token: JWTPayload) => ({
    user: token.user,
    expires: new Date(Date.now() + 86400000).toISOString(),
  })),
}));

describe('parseCookieHeader', () => {
  it('should parse a simple cookie header', () => {
    const result = parseCookieHeader('name=value');
    expect(result.get('name')).toBe('value');
  });

  it('should parse multiple cookies', () => {
    const result = parseCookieHeader('a=1; b=2; c=3');
    expect(result.get('a')).toBe('1');
    expect(result.get('b')).toBe('2');
    expect(result.get('c')).toBe('3');
  });

  it('should handle URL-encoded names and values', () => {
    const result = parseCookieHeader(`${encodeURIComponent('my cookie')}=${encodeURIComponent('hello world')}`);
    expect(result.get('my cookie')).toBe('hello world');
  });

  it('should handle empty string', () => {
    const result = parseCookieHeader('');
    expect(result.size).toBe(0);
  });
});

describe('parseCookies', () => {
  it('should parse cookies from request', () => {
    const request = new Request('http://localhost/', {
      headers: { cookie: 'a=1; b=2' },
    });
    const result = parseCookies(request);
    expect(result.get('a')).toBe('1');
    expect(result.get('b')).toBe('2');
  });

  it('should return empty map when no cookie header', () => {
    const request = new Request('http://localhost/');
    const result = parseCookies(request);
    expect(result.size).toBe(0);
  });
});

describe('parseBackendAuthResponse', () => {
  it('should parse standard API response', () => {
    const data = {
      accessToken: 'at',
      refreshToken: 'rt',
      user: { id: '1', email: 'test@test.com', name: 'Test', image: null },
    };
    const result = parseBackendAuthResponse(data);
    expect(result.tokens.accessToken).toBe('at');
    expect(result.user.email).toBe('test@test.com');
  });

  it('should use provided email and name as fallback', () => {
    const data = { accessToken: 'at', refreshToken: 'rt' };
    const result = parseBackendAuthResponse(data, 'email@example.com', 'Name');
    expect(result.user.email).toBe('email@example.com');
  });

  it('should parse roles and permissions from access token claims', () => {
    const data = {
      accessToken: makeUnsignedJwt({
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin', 'User'],
        permissions: ['users:read', 'users:create'],
      }),
      refreshToken: 'rt',
      userId: '1',
      email: 'test@test.com',
    };

    const result = parseBackendAuthResponse(data);

    expect(result.user.roles).toEqual(['Admin', 'User']);
    expect(result.user.permissions).toEqual(['users:read', 'users:create']);
  });
});

function makeConfig(overrides?: Partial<ResolvedAuthConfig>): ResolvedAuthConfig {
  return {
    providers: [
      {
        id: 'credentials',
        name: 'Credentials',
        type: 'credentials',
        authorize: vi.fn<CredentialsProviderConfig['authorize']>(async (creds) => {
          const email = creds.email;
          if (email !== undefined && email !== null && typeof email !== 'string') {
            throw new TypeError('The credentials fixture requires a string, null or absent email');
          }
          return {
            tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
            user: { id: '1', email, name: 'Test', image: null },
          };
        }),
      },
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

describe('createHandlers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('GET handler', () => {
    it('should return session for GET /api/auth/session', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/session', {
        headers: {
          cookie: '__me.session-token=valid-encrypted-token',
        },
      });

      const response = await GET(request);
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body.user).toBeDefined();
    });

    it('should return CSRF token for GET /api/auth/csrf', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/csrf');
      const response = await GET(request);
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body.csrfToken).toBe('csrf-token-value');
    });

    it('should return providers for GET /api/auth/providers', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/providers');
      const response = await GET(request);
      expect(response.status).toBe(200);
      const body = await response.json();
      // Response is an array of { id, name, type }
      expect(Array.isArray(body)).toBe(true);
      expect(body[0].id).toBe('credentials');
      expect(body[0].name).toBe('Credentials');
      expect(body[0].type).toBe('credentials');
    });

    it('should return 404 for unknown GET action', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/unknown');
      const response = await GET(request);
      expect(response.status).toBe(404);
    });

    it('should handle callback without provider', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/callback');
      const response = await GET(request);
      // Should return error about missing provider
      expect(response.status).toBeGreaterThanOrEqual(400);
    });
  });

  describe('POST handler', () => {
    it('should return 404 for unknown POST action', async () => {
      const config = makeConfig();
      const { POST } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/unknown', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      });

      const response = await POST(request);
      expect(response.status).toBe(404);
    });

    it('should fail CSRF validation with wrong token', async () => {
      const config = makeConfig();
      const { POST } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/signin', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          cookie: '__me.csrf-token=csrf-cookie-value',
        },
        body: JSON.stringify({ csrfToken: 'wrong-token' }),
      });

      const response = await POST(request);
      expect(response.status).toBeGreaterThanOrEqual(400);
    });

    it('should handle signout with valid CSRF', async () => {
      const config = makeConfig();
      const { POST } = createHandlers(config);

      // Mock fetch for token revocation
      vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('{}', { status: 200 }));

      const request = new Request('http://localhost/api/auth/signout', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          cookie: '__me.csrf-token=csrf-cookie-value',
        },
        body: JSON.stringify({ csrfToken: 'csrf-token-value' }),
      });

      const response = await POST(request);
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body.ok).toBe(true);
    });

    it('should handle signout even without a session cookie', async () => {
      const config = makeConfig();
      const { POST } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/signout', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          cookie: '__me.csrf-token=csrf-cookie-value',
        },
        body: JSON.stringify({ csrfToken: 'csrf-token-value' }),
      });

      const response = await POST(request);
      expect(response.status).toBe(200);
    });

    it('should handle session update POST', async () => {
      const config = makeConfig();
      const { POST } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/session', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          cookie: '__me.session-token=valid-encrypted-token',
        },
        body: JSON.stringify({ name: 'Updated Name' }),
      });

      const response = await POST(request);
      expect(response.status).toBe(200);
    });

    it('should enable debug logging when config.debug is true', async () => {
      const config = makeConfig({ debug: true });
      const { POST } = createHandlers(config);

      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      const request = new Request('http://localhost/api/auth/signin', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          cookie: '__me.csrf-token=csrf-cookie-value',
        },
        body: JSON.stringify({
          csrfToken: 'csrf-token-value',
          provider: 'nonexistent',
        }),
      });

      const response = await POST(request);
      // Provider doesn't exist, should fail with error
      expect(response.status).toBeGreaterThanOrEqual(400);

      consoleSpy.mockRestore();
    });
  });

  describe('OAuth callback', () => {
    it('should redirect for callback with error param', async () => {
      const config = makeConfig({
        providers: [
          legacyOAuthFixture({
            id: 'google',
            name: 'Google',
            type: 'oauth',
            handleCallback: vi.fn(),
          }),
        ],
      });
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/callback/google?error=access_denied&error_description=User%20denied');

      const response = await GET(request);
      // Error param => redirect to error page
      expect(response.status).toBe(302);
      expect(response.headers.get('location')).toContain('error=access_denied');
    });

    it('should redirect for callback without code', async () => {
      const config = makeConfig({
        providers: [
          legacyOAuthFixture({
            id: 'google',
            name: 'Google',
            type: 'oauth',
            handleCallback: vi.fn(),
          }),
        ],
      });
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/callback/google');

      const response = await GET(request);
      // Missing code => redirect to error page
      expect(response.status).toBe(302);
      expect(response.headers.get('location')).toContain('error=missing_code');
    });

    it('should return error for callback with unknown provider', async () => {
      const config = makeConfig();
      const { GET } = createHandlers(config);

      const request = new Request('http://localhost/api/auth/callback/nonexistent?code=abc');

      const response = await GET(request);
      expect(response.status).toBeGreaterThanOrEqual(400);
    });
  });
});
