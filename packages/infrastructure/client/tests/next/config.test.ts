/**
 * Tests for Next.js Auth Config Resolution
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { resolveConfig } from '../../src/integrations/next/config.js';
import type { AuthCallbacks, GameGuildAuthConfig, JWTPayload, Session, SessionUser } from '../../src/runtime/auth/types.js';
import { MissingSecretError, ConfigError } from '../../src/runtime/auth/errors.js';

describe('resolveConfig', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  function makeMinimalConfig(overrides?: Partial<GameGuildAuthConfig>): GameGuildAuthConfig {
    return {
      providers: [],
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:5000',
      ...overrides,
    };
  }

  it('should resolve minimal config with defaults', () => {
    const result = resolveConfig(makeMinimalConfig());

    expect(result.secret).toBe('test-secret-min-32-chars-long-ok');
    expect(result.apiUrl).toBe('http://localhost:5000');
    expect(result.providers).toEqual([]);
    expect(result.maxAge).toBe(30 * 24 * 60 * 60);
    expect(result.updateAge).toBe(0);
    expect(result.basePath).toBe('/api/auth');
    expect(result.debug).toBe(false);
    expect(result.trustHost).toBe(false);
    expect(result.tenantHeader).toBe('X-Tenant-Id');
  });

  it('should resolve cookie defaults', () => {
    const result = resolveConfig(makeMinimalConfig());

    expect(result.cookies.name).toBe('__me');
    expect(result.cookies.secure).toBe(false);
    expect(result.cookies.sameSite).toBe('lax');
    expect(result.cookies.path).toBe('/');
    expect(result.cookies.httpOnly).toBe(true);
  });

  it('should throw MissingSecretError when no secret available', () => {
    delete process.env.AUTH_SECRET;
    delete process.env.NEXTAUTH_SECRET;

    expect(() => resolveConfig({ providers: [], apiUrl: 'http://localhost:5000' })).toThrow(MissingSecretError);
  });

  it('should throw ConfigError when no apiUrl available', () => {
    delete process.env.API_URL;
    delete process.env.NEXT_PUBLIC_API_URL;

    expect(() => resolveConfig({ providers: [], secret: 'test-secret-min-32-chars-long-ok' })).toThrow(ConfigError);
  });

  it('should read secret from AUTH_SECRET environment variable', () => {
    process.env.AUTH_SECRET = 'env-secret-that-is-long-enough-32';

    const result = resolveConfig({
      providers: [],
      apiUrl: 'http://localhost:5000',
    });

    expect(result.secret).toBe('env-secret-that-is-long-enough-32');
  });

  it('should read secret from NEXTAUTH_SECRET fallback', () => {
    delete process.env.AUTH_SECRET;
    process.env.NEXTAUTH_SECRET = 'nextauth-secret-long-enough-32ch';

    const result = resolveConfig({
      providers: [],
      apiUrl: 'http://localhost:5000',
    });

    expect(result.secret).toBe('nextauth-secret-long-enough-32ch');
  });

  it('should read apiUrl from API_URL environment variable', () => {
    process.env.API_URL = 'http://env-api:5000';

    const result = resolveConfig({
      providers: [],
      secret: 'test-secret-min-32-chars-long-ok',
    });

    expect(result.apiUrl).toBe('http://env-api:5000');
  });

  it('should read apiUrl from NEXT_PUBLIC_API_URL fallback', () => {
    delete process.env.API_URL;
    process.env.NEXT_PUBLIC_API_URL = 'http://public-api:5000';

    const result = resolveConfig({
      providers: [],
      secret: 'test-secret-min-32-chars-long-ok',
    });

    expect(result.apiUrl).toBe('http://public-api:5000');
  });

  it('should detect secure from NEXTAUTH_URL starting with https', () => {
    process.env.NEXTAUTH_URL = 'https://myapp.com';

    const result = resolveConfig(makeMinimalConfig());

    expect(result.cookies.secure).toBe(true);
  });

  it('should allow overriding secure in cookies config', () => {
    const result = resolveConfig(makeMinimalConfig({ cookies: { secure: true } }));

    expect(result.cookies.secure).toBe(true);
  });

  it('should merge custom callbacks', () => {
    const customJwt = vi.fn<NonNullable<AuthCallbacks['jwt']>>(async ({ token }) => token);

    const result = resolveConfig(
      makeMinimalConfig({
        callbacks: { jwt: customJwt },
      }),
    );

    expect(result.callbacks.jwt).toBe(customJwt);
    // Other callbacks should use defaults
    expect(result.callbacks.session).toBeDefined();
    expect(result.callbacks.signIn).toBeDefined();
    expect(result.callbacks.redirect).toBeDefined();
    expect(result.callbacks.authorized).toBeDefined();
  });

  it('should use custom pages', () => {
    const result = resolveConfig(
      makeMinimalConfig({
        pages: { signIn: '/login', error: '/auth/error' },
      }),
    );

    expect(result.pages).toEqual({ signIn: '/login', error: '/auth/error' });
  });

  it('should use custom basePath', () => {
    const result = resolveConfig(makeMinimalConfig({ basePath: '/auth' }));

    expect(result.basePath).toBe('/auth');
  });

  it('should use custom maxAge and updateAge', () => {
    const result = resolveConfig(makeMinimalConfig({ maxAge: 3600, updateAge: 300 }));

    expect(result.maxAge).toBe(3600);
    expect(result.updateAge).toBe(300);
  });

  it('should use custom cookie settings', () => {
    const result = resolveConfig(
      makeMinimalConfig({
        cookies: {
          name: '__custom',
          sameSite: 'strict',
          path: '/app',
          domain: 'example.com',
          maxAge: 7200,
          httpOnly: false,
        },
      }),
    );

    expect(result.cookies.name).toBe('__custom');
    expect(result.cookies.sameSite).toBe('strict');
    expect(result.cookies.path).toBe('/app');
    expect(result.cookies.domain).toBe('example.com');
    expect(result.cookies.maxAge).toBe(7200);
    expect(result.cookies.httpOnly).toBe(false);
  });

  describe('default callbacks behavior', () => {
    // Deliberately partial inputs characterize identity/boolean callbacks here;
    // they are not valid persisted sessions or encrypted JWT payloads.
    it('jwt callback should pass through token', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const token: Partial<JWTPayload> = { user: { id: '1' } };
      const output = await result.callbacks.jwt({ token: token as JWTPayload });
      expect(output).toBe(token);
    });

    it('session callback should pass through session', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const session: Partial<Session> = { user: { id: '1' } };
      const ignoredToken: Partial<JWTPayload> = {};
      const output = await result.callbacks.session({ session: session as Session, token: ignoredToken as JWTPayload });
      expect(output).toBe(session);
    });

    it('signIn callback should return true', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const ignoredUser: Partial<SessionUser> = {};
      const output = await result.callbacks.signIn({ user: ignoredUser as SessionUser, provider: 'credentials' });
      expect(output).toBe(true);
    });

    it('authorized callback should return true when auth exists', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const auth: Partial<Session> = { user: { id: '1' } };
      const output = await result.callbacks.authorized({
        auth: auth as Session,
        request: new Request('http://localhost/'),
      });
      expect(output).toBe(true);
    });

    it('authorized callback should return false when auth is null', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const output = await result.callbacks.authorized({
        auth: null,
        request: new Request('http://localhost/'),
      });
      expect(output).toBe(false);
    });

    it('redirect callback should allow relative URLs', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const output = await result.callbacks.redirect({
        url: '/dashboard',
        baseUrl: 'http://localhost:3000',
      });
      expect(output).toBe('http://localhost:3000/dashboard');
    });

    it('redirect callback should allow same-origin URLs', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const output = await result.callbacks.redirect({
        url: 'http://localhost:3000/profile',
        baseUrl: 'http://localhost:3000',
      });
      expect(output).toBe('http://localhost:3000/profile');
    });

    it('redirect callback should block cross-origin URLs', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const output = await result.callbacks.redirect({
        url: 'http://evil.com/phish',
        baseUrl: 'http://localhost:3000',
      });
      expect(output).toBe('http://localhost:3000');
    });

    it('redirect callback should handle invalid URLs gracefully', async () => {
      const result = resolveConfig(makeMinimalConfig());
      const output = await result.callbacks.redirect({
        url: 'not a valid url',
        baseUrl: 'http://localhost:3000',
      });
      expect(output).toBe('http://localhost:3000');
    });
  });
});
