/**
 * Tests for GameGuildAuth factory
 */

import { describe, it, expect, vi } from 'vitest';
import { GameGuildAuth } from '../../src/integrations/next/auth.js';
import type { AuthCallbacks, CredentialsProviderConfig, GameGuildAuthConfig, ResolvedAuthConfig } from '../../src/runtime/auth/types.js';

// Mock dependencies
vi.mock('../../src/integrations/next/config.js', () => ({
  resolveConfig: vi.fn((config: GameGuildAuthConfig): ResolvedAuthConfig => ({
    providers: config.providers || [],
    callbacks: {
      jwt: config.callbacks?.jwt ?? (async ({ token }) => token),
      session: config.callbacks?.session ?? (async ({ session }) => session),
      signIn: config.callbacks?.signIn ?? (async () => true),
      redirect: config.callbacks?.redirect ?? (async ({ url }) => url),
      authorized: config.callbacks?.authorized ?? (async ({ auth }) => !!auth),
    },
    secret: config.secret || 'test-secret',
    apiUrl: config.apiUrl || 'http://localhost:5000',
    pages: config.pages ?? {},
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
  })),
}));

vi.mock('../../src/integrations/next/handlers.js', () => ({
  createHandlers: vi.fn(() => ({
    GET: vi.fn(async () => new Response('GET')),
    POST: vi.fn(async () => new Response('POST')),
  })),
  parseCookieHeader: vi.fn(() => new Map()),
}));

vi.mock('../../src/integrations/next/actions.js', () => ({
  createAuthFunction: vi.fn(() => {
    const fn = vi.fn(async () => null);
    return fn;
  }),
  createSignInAction: vi.fn(() => vi.fn(async () => {})),
  createSignUpAction: vi.fn(() => vi.fn(async () => {})),
  createSignOutAction: vi.fn(() => vi.fn(async () => {})),
  createUpdateAction: vi.fn(() => vi.fn(async () => null)),
}));

describe('GameGuildAuth', () => {
  it('should return an auth instance with all utilities', () => {
    const result = GameGuildAuth({
      providers: [],
      secret: 'test-secret-min-32-chars-long-ok',
      apiUrl: 'http://localhost:5000',
    });

    expect(result).toBeDefined();
    expect(result.handlers).toBeDefined();
    expect(result.handlers.GET).toBeDefined();
    expect(result.handlers.POST).toBeDefined();
    expect(result.auth).toBeDefined();
    expect(result.signIn).toBeDefined();
    expect(result.signUp).toBeDefined();
    expect(result.signOut).toBeDefined();
    expect(result.update).toBeDefined();
    expect(result.config).toBeDefined();
  });

  it('should pass config through resolveConfig', () => {
    const customJwt = vi.fn(async ({ token }: Parameters<NonNullable<AuthCallbacks['jwt']>>[0]) => token);

    const result = GameGuildAuth({
      providers: [],
      secret: 'test-secret',
      apiUrl: 'http://api:5000',
      callbacks: { jwt: customJwt },
    });

    expect(result.config).toBeDefined();
    expect(result.config.apiUrl).toBe('http://api:5000');
  });

  it('should create handlers with resolved config', () => {
    const result = GameGuildAuth({
      providers: [
        {
          id: 'credentials',
          name: 'Credentials',
          type: 'credentials',
          authorize: vi.fn<CredentialsProviderConfig['authorize']>(),
        },
      ],
      secret: 'test-secret',
      apiUrl: 'http://localhost:5000',
    });

    expect(typeof result.handlers.GET).toBe('function');
    expect(typeof result.handlers.POST).toBe('function');
  });

  it('should create all server actions', () => {
    const result = GameGuildAuth({
      providers: [],
      secret: 'test-secret',
      apiUrl: 'http://localhost:5000',
    });

    expect(typeof result.signIn).toBe('function');
    expect(typeof result.signUp).toBe('function');
    expect(typeof result.signOut).toBe('function');
    expect(typeof result.update).toBe('function');
  });
});
