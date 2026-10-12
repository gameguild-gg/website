/**
 * Extended Session Tests — processSession, encodeSession, refreshAccessToken
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { processSession, encodeSession, refreshAccessToken, createJWTPayload, toSession, shouldRefreshToken } from '../../src/runtime/auth/session.js';
import type { JWTPayload, ResolvedAuthConfig, ProviderResult } from '../../src/runtime/auth/types.js';
import { TokenRefreshError } from '../../src/runtime/auth/errors.js';
import { decodeJWT } from '../../src/runtime/auth/jwt.js';

// Mock JWT module
vi.mock('../../src/runtime/auth/jwt.js', () => ({
  decodeJWT: vi.fn(async ({ token, secret }: any) => {
    if (token === 'valid-encrypted') {
      return {
        user: { id: '1', email: 'test@ex.com', name: 'Test', image: null },
        accessToken: 'at',
        refreshToken: 'rt',
        accessTokenExpires: Date.now() + 3600000,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      };
    }
    if (token === 'needs-refresh') {
      return {
        user: { id: '1', email: 'test@ex.com', name: 'Test', image: null },
        accessToken: 'old-at',
        refreshToken: 'rt',
        accessTokenExpires: Date.now() - 60000, // expired
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 86400,
      };
    }
    if (token === 'outer-expired') {
      return {
        user: { id: '1', email: 'test@ex.com', name: 'Test', image: null },
        accessToken: 'at',
        refreshToken: 'rt',
        accessTokenExpires: Date.now() + 3600000,
        iat: Math.floor(Date.now() / 1000) - 100000,
        exp: Math.floor(Date.now() / 1000) - 100,
      };
    }
    return null;
  }),
  encodeJWT: vi.fn(async ({ token, secret, maxAge }: any) => 'encoded-jwt'),
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
    secret: 'test-secret-32-chars-long-enough',
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

describe('processSession', () => {
  it.each([401, 403])('clears a near-expiry session when refresh is denied with %s', async (status) => {
    vi.mocked(decodeJWT).mockResolvedValueOnce({
      user: { id: 'synthetic-owner' }, accessToken: 'synthetic-access', refreshToken: 'synthetic-refresh',
      accessTokenExpires: Date.now() + 15_000, exp: Math.floor(Date.now() / 1000) + 3600,
    });
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response(null, { status }));
    const config = makeConfig();
    const jwtCallback = vi.spyOn(config.callbacks, 'jwt');
    try {
      const result = await processSession('synthetic-near-expiry', config);
      expect(result).toEqual({ session: null, token: null, updated: false });
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(jwtCallback).not.toHaveBeenCalled();
    } finally {
      fetchMock.mockRestore();
      jwtCallback.mockRestore();
    }
  });

  it('preserves a still-valid session when refresh returns temporary 503', async () => {
    vi.mocked(decodeJWT).mockResolvedValueOnce({
      user: { id: 'synthetic-owner' }, accessToken: 'synthetic-access', refreshToken: 'synthetic-refresh',
      accessTokenExpires: Date.now() + 15_000, exp: Math.floor(Date.now() / 1000) + 3600,
    });
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response(null, { status: 503 }));
    try {
      const result = await processSession('synthetic-near-expiry', makeConfig());
      expect(result.session?.user.id).toBe('synthetic-owner');
      expect(result.token?.accessToken).toBe('synthetic-access');
      expect(result.updated).toBe(false);
      expect(fetchMock).toHaveBeenCalledTimes(1);
    } finally {
      fetchMock.mockRestore();
    }
  });

  it('should return session for valid token', async () => {
    const result = await processSession('valid-encrypted', makeConfig());

    expect(result.session).toBeDefined();
    expect(result.session?.user.id).toBe('1');
    expect(result.token).toBeDefined();
    expect(result.updated).toBe(false);
  });

  it('should return null session for invalid token', async () => {
    const result = await processSession('invalid-token', makeConfig());

    expect(result.session).toBeNull();
    expect(result.token).toBeNull();
    expect(result.updated).toBe(false);
  });

  it('should return null session for outer-expired token', async () => {
    const result = await processSession('outer-expired', makeConfig());

    expect(result.session).toBeNull();
    expect(result.token).toBeNull();
    expect(result.updated).toBe(false);
  });

  it('should attempt token refresh when access token expired', async () => {
    // Mock the fetch for token refresh
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'new-at',
          refreshToken: 'new-rt',
          expiresIn: 3600,
        }),
        { status: 200 },
      ),
    );

    const result = await processSession('needs-refresh', makeConfig());

    expect(result.session).toBeDefined();
    expect(result.updated).toBe(true);

    mockFetch.mockRestore();
  });

  it('clears an expired access session if refresh fails despite a valid outer JWT', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockRejectedValueOnce(new Error('Network error'));
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    const result = await processSession('needs-refresh', makeConfig({ debug: true }));

    expect(result.session).toBeNull();
    expect(result.updated).toBe(false);

    mockFetch.mockRestore();
    warnSpy.mockRestore();
  });

  it('should detect jwt callback modifications and mark as updated', async () => {
    const modifiedToken = {
      user: { id: '1', email: 'test@ex.com', name: 'Modified', image: null },
      accessToken: 'at',
      refreshToken: 'rt',
      accessTokenExpires: Date.now() + 3600000,
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + 86400,
    };

    const config = makeConfig({
      callbacks: {
        jwt: async () => modifiedToken,
        session: async ({ session }) => session,
        signIn: async () => true,
        redirect: async ({ url }) => url,
        authorized: async ({ auth }) => !!auth,
      },
    });

    const result = await processSession('valid-encrypted', config);

    expect(result.session).toBeDefined();
    expect(result.updated).toBe(true);
  });
});

describe('encodeSession', () => {
  it('should encode a token', async () => {
    const token = {
      user: { id: '1', email: 'a@b.com', name: 'T', image: null },
      accessToken: 'at',
      refreshToken: 'rt',
      accessTokenExpires: Date.now() + 3600000,
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + 86400,
    } as JWTPayload;

    const result = await encodeSession(token, makeConfig());

    expect(result).toBe('encoded-jwt');
  });
});

describe('refreshAccessToken', () => {
  it('should throw when no refresh token', async () => {
    const token = { refreshToken: '' } as JWTPayload;
    await expect(refreshAccessToken(token, makeConfig())).rejects.toThrow('No refresh token available');
  });

  it('should refresh token successfully', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'new-at',
          refreshToken: 'new-rt',
          expiresIn: 3600,
          refreshTokenExpiresAt: '2025-12-31T00:00:00Z',
          tenantId: 'tenant-2',
          availableTenants: [{ id: 'tenant-2', name: 'Production' }],
        }),
        { status: 200 },
      ),
    );

    const token = {
      user: { id: '1' },
      accessToken: 'old-at',
      refreshToken: 'old-rt',
      accessTokenExpires: Date.now() - 1000,
      tenantId: 'tenant-1',
      availableTenants: [{ id: 'tenant-1', name: 'Development' }],
    } as JWTPayload;

    const result = await refreshAccessToken(token, makeConfig());

    expect(result.accessToken).toBe('new-at');
    expect(result.refreshToken).toBe('new-rt');
    expect(result.accessTokenExpires).toBeGreaterThan(Date.now());
    expect(result.tenantId).toBe('tenant-2');
    expect(result.availableTenants).toEqual([{ id: 'tenant-2', name: 'Production' }]);
    expect(mockFetch).toHaveBeenCalledWith(
      'http://localhost:8080/v1/auth/tokens:refresh',
      expect.objectContaining({ body: JSON.stringify({ refreshToken: 'old-rt', tenantId: 'tenant-1' }) }),
    );

    mockFetch.mockRestore();
  });

  it('should throw TokenRefreshError on non-ok response', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(new Response('', { status: 401 }));

    const token = { refreshToken: 'rt' } as JWTPayload;

    await expect(refreshAccessToken(token, makeConfig())).rejects.toThrow(TokenRefreshError);

    mockFetch.mockRestore();
  });

  it('should throw TokenRefreshError on network error', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockRejectedValueOnce(new Error('Network error'));

    const token = { refreshToken: 'rt' } as JWTPayload;

    await expect(refreshAccessToken(token, makeConfig())).rejects.toThrow(TokenRefreshError);

    mockFetch.mockRestore();
  });

  it('should use accessTokenExpiresAt when available', async () => {
    const futureDate = new Date(Date.now() + 7200000).toISOString();
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'new-at',
          accessTokenExpiresAt: futureDate,
        }),
        { status: 200 },
      ),
    );

    const token = { refreshToken: 'rt', accessToken: 'at' } as JWTPayload;

    const result = await refreshAccessToken(token, makeConfig());

    expect(result.accessTokenExpires).toBe(new Date(futureDate).getTime());

    mockFetch.mockRestore();
  });

  it('should default to 1 hour expiry when no expiry info', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'new-at',
        }),
        { status: 200 },
      ),
    );

    const token = { refreshToken: 'rt', accessToken: 'at' } as JWTPayload;

    const result = await refreshAccessToken(token, makeConfig());

    // Should be about 1 hour from now
    const oneHour = 60 * 60 * 1000;
    expect(result.accessTokenExpires).toBeGreaterThan(Date.now() + oneHour - 5000);

    mockFetch.mockRestore();
  });

  it('should keep old refresh token if new one not provided', async () => {
    const mockFetch = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          accessToken: 'new-at',
          expiresIn: 3600,
        }),
        { status: 200 },
      ),
    );

    const token = { refreshToken: 'keep-this-rt', accessToken: 'at' } as JWTPayload;

    const result = await refreshAccessToken(token, makeConfig());

    expect(result.refreshToken).toBe('keep-this-rt');

    mockFetch.mockRestore();
  });
});

describe('createJWTPayload', () => {
  it('should create payload with accessTokenExpiresAt', () => {
    const expiresAt = new Date(Date.now() + 3600000).toISOString();
    const result: ProviderResult = {
      tokens: {
        accessToken: 'at',
        refreshToken: 'rt',
        tokenType: 'Bearer',
        accessTokenExpiresAt: expiresAt,
        refreshTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
      },
      user: { id: '1', email: 'a@b.com', name: 'T', image: null },
      sessionId: 'sid',
      tenantId: 'tid',
      availableTenants: [{ id: 't1', name: 'T1' }],
    };

    const payload = createJWTPayload(result, makeConfig());

    expect(payload.accessToken).toBe('at');
    expect(payload.refreshToken).toBe('rt');
    expect(payload.accessTokenExpires).toBe(new Date(expiresAt).getTime());
    expect(payload.refreshTokenExpires).toBeDefined();
    expect(payload.sessionId).toBe('sid');
    expect(payload.tenantId).toBe('tid');
    expect(payload.availableTenants).toEqual([{ id: 't1', name: 'T1' }]);
  });

  it('should create payload with expiresIn', () => {
    const result: ProviderResult = {
      tokens: {
        accessToken: 'at',
        refreshToken: 'rt',
        tokenType: 'Bearer',
        expiresIn: 3600,
      },
      user: { id: '1', email: 'a@b.com', name: 'T', image: null },
    };

    const payload = createJWTPayload(result, makeConfig());

    expect(payload.accessTokenExpires).toBeGreaterThan(Date.now());
  });

  it('should default to 1 hour when no expiry info', () => {
    const result: ProviderResult = {
      tokens: {
        accessToken: 'at',
        refreshToken: 'rt',
        tokenType: 'Bearer',
      },
      user: { id: '1', email: 'a@b.com', name: 'T', image: null },
    };

    const payload = createJWTPayload(result, makeConfig());

    const oneHour = 60 * 60 * 1000;
    expect(payload.accessTokenExpires).toBeGreaterThan(Date.now() + oneHour - 5000);
  });
});

describe('toSession', () => {
  it('should convert token to session without exposing tokens', () => {
    const token: JWTPayload = {
      user: { id: '1', email: 'a@b.com', name: 'T', image: null, roles: ['admin'] },
      accessToken: 'SHOULD_NOT_APPEAR',
      refreshToken: 'SHOULD_NOT_APPEAR',
      accessTokenExpires: Date.now() + 3600000,
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + 86400,
      tenantId: 'tid',
      availableTenants: [{ id: 't1', name: 'T1' }],
    };

    const session = toSession(token);

    expect(session.user.id).toBe('1');
    expect(session.user.roles).toEqual(['admin']);
    expect(session.tenantId).toBe('tid');
    expect(session.availableTenants).toEqual([{ id: 't1', name: 'T1' }]);
    expect((session as any).accessToken).toBeUndefined();
    expect((session as any).refreshToken).toBeUndefined();
    expect(session.expires).toBeDefined();
  });

  it('should default expires to 30 days when no exp', () => {
    const token = {
      user: { id: '1', email: 'a@b.com', name: null, image: null },
      accessToken: 'at',
      refreshToken: 'rt',
      accessTokenExpires: Date.now() + 3600000,
    } as JWTPayload;

    const session = toSession(token);

    const thirtyDays = 30 * 24 * 60 * 60 * 1000;
    const expires = new Date(session.expires).getTime();
    expect(expires).toBeGreaterThan(Date.now() + thirtyDays - 5000);
  });
});

describe('shouldRefreshToken', () => {
  it('should return false when no accessTokenExpires', () => {
    const token = {} as JWTPayload;
    expect(shouldRefreshToken(token)).toBe(false);
  });

  it('should return false when token not near expiry', () => {
    const token = {
      accessTokenExpires: Date.now() + 3600000, // 1 hour
    } as JWTPayload;
    expect(shouldRefreshToken(token)).toBe(false);
  });

  it('should return true when token about to expire', () => {
    const token = {
      accessTokenExpires: Date.now() + 10000, // 10 seconds
    } as JWTPayload;
    expect(shouldRefreshToken(token)).toBe(true);
  });

  it('should return true when token already expired', () => {
    const token = {
      accessTokenExpires: Date.now() - 1000,
    } as JWTPayload;
    expect(shouldRefreshToken(token)).toBe(true);
  });
});


describe('refreshAccessToken coverage completion', () => {
  function completeToken(refreshToken: string): JWTPayload {
    return {
      user: {
        id: 'existing-id',
        email: 'existing@example.com',
        name: 'Existing User',
        image: 'https://example.com/existing.png',
        roles: ['existing-role'],
        permissions: ['existing:read'],
      },
      accessToken: 'old-access',
      refreshToken,
      accessTokenExpires: Date.now() - 1,
      tenantId: 'tenant-1',
    };
  }

  it('deduplicates concurrent refresh requests for the same token', async () => {
    const fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ accessToken: 'new-access', expiresIn: 3600 }), { status: 200 }),
    );
    const token = completeToken('shared-refresh');

    const [first, second] = await Promise.all([refreshAccessToken(token, makeConfig()), refreshAccessToken(token, makeConfig())]);

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    expect(first.accessToken).toBe('new-access');
    expect(second).toEqual(first);
    fetchSpy.mockRestore();
  });

  it('maps refreshed user metadata and preserves every supported fallback', async () => {
    const responses = [
      {
        accessToken: 'access-1',
        user: {
          id: 'new-id',
          email: 'new@example.com',
          displayName: 'Display Name',
          profilePictureUrl: 'https://example.com/profile.png',
        },
        roles: ['admin'],
        permissions: ['users:write'],
      },
      {
        accessToken: 'access-2',
        user: {
          email: 42,
          username: 'username-fallback',
          image: 'https://example.com/image.png',
        },
      },
      {
        accessToken: 'access-3',
        user: {
          email: null,
          name: null,
          image: null,
        },
      },
      {
        accessToken: 'access-4',
        user: {},
      },
      {
        accessToken: 'access-5',
        user: [],
      },
      {
        accessToken: 'access-6',
        user: null,
      },
    ];

    const fetchSpy = vi.spyOn(global, 'fetch');
    for (const response of responses) {
      fetchSpy.mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));
    }

    const display = await refreshAccessToken(completeToken('refresh-1'), makeConfig());
    const username = await refreshAccessToken(completeToken('refresh-2'), makeConfig());
    const nullable = await refreshAccessToken(completeToken('refresh-3'), makeConfig());
    const empty = await refreshAccessToken(completeToken('refresh-4'), makeConfig());
    const arrayUser = await refreshAccessToken(completeToken('refresh-5'), makeConfig());
    const nullUser = await refreshAccessToken(completeToken('refresh-6'), makeConfig());

    expect(display.user).toMatchObject({
      id: 'new-id',
      email: 'new@example.com',
      name: 'Display Name',
      image: 'https://example.com/profile.png',
      roles: ['admin'],
      permissions: ['users:write'],
    });
    expect(username.user).toMatchObject({
      id: 'existing-id',
      email: 'existing@example.com',
      name: 'username-fallback',
      image: 'https://example.com/image.png',
    });
    expect(nullable.user).toMatchObject({ email: null, name: null, image: null });
    expect(empty.user).toMatchObject({
      id: 'existing-id',
      email: 'existing@example.com',
      name: 'Existing User',
      image: 'https://example.com/existing.png',
    });
    expect(arrayUser.user).toEqual(completeToken('ignored').user);
    expect(nullUser.user).toEqual(completeToken('ignored').user);
    fetchSpy.mockRestore();
  });
});
