/**
 * Session Management Tests
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createJWTPayload, toSession, shouldRefreshToken, refreshAccessToken } from '../../src/runtime/auth/session.js';
import { TokenRefreshError } from '../../src/runtime/auth/errors.js';
import type { ProviderResult, ResolvedAuthConfig, JWTPayload } from '../../src/runtime/auth/types.js';

// Mocks
vi.mock('../../src/runtime/auth/jwt.js', () => ({
  encodeJWT: vi.fn().mockResolvedValue('encrypted-token'),
  decodeJWT: vi.fn().mockResolvedValue(null),
}));

const mockConfig: ResolvedAuthConfig = {
  providers: [],
  callbacks: {
    jwt: async ({ token }) => token,
    session: async ({ session }) => session,
    signIn: async () => true,
    redirect: async ({ url }) => url,
    authorized: async () => true,
  },
  secret: 'test-secret-at-least-32-characters-long',
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
};

interface RefreshResponseFixture {
  ok: boolean;
  status?: number;
  json?: () => Promise<{
    accessToken: string;
    refreshToken?: string;
    expiresIn?: number;
    accessTokenExpiresAt?: string;
    tenantId?: string;
  }>;
}
type RefreshFetchFixture = (...args: Parameters<typeof fetch>) => Promise<RefreshResponseFixture>;

function stubRefreshResponse(response: RefreshResponseFixture) {
  const mockFetch = vi.fn<RefreshFetchFixture>().mockResolvedValue(response);
  vi.stubGlobal('fetch', mockFetch);
  return mockFetch;
}

describe('Session Management', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('createJWTPayload', () => {
    it('should create a JWT payload from a provider result with accessTokenExpiresAt', () => {
      const expiresAt = new Date(Date.now() + 3600000).toISOString();
      const result: ProviderResult = {
        tokens: {
          accessToken: 'access-token-123',
          refreshToken: 'refresh-token-456',
          accessTokenExpiresAt: expiresAt,
          tokenType: 'Bearer',
        },
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'Test User',
        },
        sessionId: 'session-1',
        tenantId: 'tenant-1',
        availableTenants: [{ id: 'tenant-1', name: 'Default' }],
      };

      const payload = createJWTPayload(result, mockConfig);

      expect(payload.user.id).toBe('user-1');
      expect(payload.user.email).toBe('test@example.com');
      expect(payload.accessToken).toBe('access-token-123');
      expect(payload.refreshToken).toBe('refresh-token-456');
      expect(payload.sessionId).toBe('session-1');
      expect(payload.tenantId).toBe('tenant-1');
      expect(payload.availableTenants).toHaveLength(1);
      expect(payload.accessTokenExpires).toBe(new Date(expiresAt).getTime());
      expect(payload.iat).toBeDefined();
      expect(payload.exp).toBeDefined();
    });

    it('should calculate accessTokenExpires from expiresIn', () => {
      const now = Date.now();
      const result: ProviderResult = {
        tokens: {
          accessToken: 'token',
          expiresIn: 3600,
          tokenType: 'Bearer',
        },
        user: { id: 'user-1' },
      };

      const payload = createJWTPayload(result, mockConfig);

      expect(payload.accessTokenExpires).toBeGreaterThanOrEqual(now + 3600 * 1000 - 100);
      expect(payload.accessTokenExpires).toBeLessThanOrEqual(now + 3600 * 1000 + 100);
    });

    it('should default to 1 hour if no expiry info', () => {
      const now = Date.now();
      const result: ProviderResult = {
        tokens: {
          accessToken: 'token',
          tokenType: 'Bearer',
        },
        user: { id: 'user-1' },
      };

      const payload = createJWTPayload(result, mockConfig);

      expect(payload.accessTokenExpires).toBeGreaterThanOrEqual(now + 3600000 - 100);
    });

    it('should set refreshTokenExpires from refreshTokenExpiresAt', () => {
      const refreshExpiresAt = new Date(Date.now() + 86400000).toISOString();
      const result: ProviderResult = {
        tokens: {
          accessToken: 'token',
          refreshToken: 'refresh',
          refreshTokenExpiresAt: refreshExpiresAt,
          tokenType: 'Bearer',
        },
        user: { id: 'user-1' },
      };

      const payload = createJWTPayload(result, mockConfig);

      expect(payload.refreshTokenExpires).toBe(new Date(refreshExpiresAt).getTime());
    });

    it('should handle missing refreshToken', () => {
      const result: ProviderResult = {
        tokens: {
          accessToken: 'token',
          tokenType: 'Bearer',
        },
        user: { id: 'user-1' },
      };

      const payload = createJWTPayload(result, mockConfig);

      expect(payload.refreshToken).toBe('');
    });
  });

  describe('toSession', () => {
    it('should convert JWT payload to client-safe session', () => {
      const token: JWTPayload = {
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'Test',
          image: 'https://example.com/avatar.png',
          roles: ['admin'],
          permissions: ['read', 'write'],
        },
        accessToken: 'secret-token',
        refreshToken: 'secret-refresh',
        accessTokenExpires: Date.now() + 3600000,
        sessionId: 'session-1',
        tenantId: 'tenant-1',
        availableTenants: [{ id: 'tenant-1', name: 'Default' }],
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 2592000,
      };

      const session = toSession(token);

      expect(session.user.id).toBe('user-1');
      expect(session.user.email).toBe('test@example.com');
      expect(session.user.name).toBe('Test');
      expect(session.user.image).toBe('https://example.com/avatar.png');
      expect(session.user.roles).toEqual(['admin']);
      expect(session.user.permissions).toEqual(['read', 'write']);
      expect(session.tenantId).toBe('tenant-1');
      expect(session.availableTenants).toHaveLength(1);
      expect(session.expires).toBeDefined();
      // Should NOT contain tokens
      expect(Reflect.get(session, 'accessToken')).toBeUndefined();
      expect(Reflect.get(session, 'refreshToken')).toBeUndefined();
      expect(Object.hasOwn(session, 'accessToken')).toBe(false);
      expect(Object.hasOwn(session, 'refreshToken')).toBe(false);
    });

    it('should use default expiry when exp is missing', () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now() + 3600000,
      };

      const session = toSession(token);

      // Default is 30 days from now
      const expiresDate = new Date(session.expires);
      expect(expiresDate.getTime()).toBeGreaterThan(Date.now());
    });
  });

  describe('shouldRefreshToken', () => {
    it('should return false if no accessTokenExpires', () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: 0,
      };

      expect(shouldRefreshToken(token)).toBe(false);
    });

    it('should return true if token expires within 30 seconds', () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now() + 10000, // 10s from now
      };

      expect(shouldRefreshToken(token)).toBe(true);
    });

    it('should return false if token expires far in the future', () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now() + 3600000, // 1h from now
      };

      expect(shouldRefreshToken(token)).toBe(false);
    });

    it('should return true if token is already expired', () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now() - 1000, // expired 1s ago
      };

      expect(shouldRefreshToken(token)).toBe(true);
    });
  });

  describe('refreshAccessToken', () => {
    it('should throw if no refresh token', async () => {
      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: '',
        accessTokenExpires: Date.now(),
      };

      await expect(refreshAccessToken(token, mockConfig)).rejects.toThrow(TokenRefreshError);
    });

    it('should call the refresh endpoint', async () => {
      const mockFetch = stubRefreshResponse({
        ok: true,
        json: async () => ({
          accessToken: 'new-access-token',
          refreshToken: 'new-refresh-token',
          expiresIn: 3600,
        }),
      });

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'old-token',
        refreshToken: 'old-refresh',
        accessTokenExpires: Date.now(),
      };

      const result = await refreshAccessToken(token, mockConfig);

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/v1/auth/tokens:refresh',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ refreshToken: 'old-refresh' }),
        }),
      );
      expect(result.accessToken).toBe('new-access-token');
      expect(result.refreshToken).toBe('new-refresh-token');
    });

    it('should throw on non-ok response', async () => {
      stubRefreshResponse({
        ok: false,
        status: 401,
      });

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now(),
      };

      await expect(refreshAccessToken(token, mockConfig)).rejects.toThrow(TokenRefreshError);
    });

    it('should throw on fetch error', async () => {
      vi.stubGlobal('fetch', vi.fn<RefreshFetchFixture>().mockRejectedValue(new Error('Network error')));

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'token',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now(),
      };

      await expect(refreshAccessToken(token, mockConfig)).rejects.toThrow(TokenRefreshError);
    });

    it('should use accessTokenExpiresAt if provided', async () => {
      const expiresAt = new Date(Date.now() + 7200000).toISOString();
      stubRefreshResponse({
        ok: true,
        json: async () => ({
          accessToken: 'new-token',
          accessTokenExpiresAt: expiresAt,
        }),
      });

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'old',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now(),
      };

      const result = await refreshAccessToken(token, mockConfig);

      expect(result.accessTokenExpires).toBe(new Date(expiresAt).getTime());
    });

    it('should default to 1 hour when no expiry info returned', async () => {
      const now = Date.now();
      stubRefreshResponse({
        ok: true,
        json: async () => ({
          accessToken: 'new-token',
        }),
      });

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'old',
        refreshToken: 'refresh',
        accessTokenExpires: Date.now(),
      };

      const result = await refreshAccessToken(token, mockConfig);

      expect(result.accessTokenExpires).toBeGreaterThanOrEqual(now + 3600000 - 200);
    });

    it('should preserve original refreshToken if none returned', async () => {
      stubRefreshResponse({
        ok: true,
        json: async () => ({ accessToken: 'new-token' }),
      });

      const token: JWTPayload = {
        user: { id: 'user-1' },
        accessToken: 'old',
        refreshToken: 'original-refresh',
        accessTokenExpires: Date.now(),
      };

      const result = await refreshAccessToken(token, mockConfig);

      expect(result.refreshToken).toBe('original-refresh');
    });

    it('should replace tenant-scoped roles and permissions from the refreshed access token', async () => {
      const encodedClaims = Buffer.from(
        JSON.stringify({
          role: 'Member',
          permissions: ['support:read'],
        }),
      ).toString('base64url');
      const refreshedAccessToken = `header.${encodedClaims}.signature`;

      stubRefreshResponse({
        ok: true,
        json: async () => ({
          accessToken: refreshedAccessToken,
          refreshToken: 'tenant-b-refresh',
          tenantId: 'tenant-b',
          expiresIn: 3600,
        }),
      });

      const token: JWTPayload = {
        user: {
          id: 'user-1',
          roles: ['PropertyOwner'],
          permissions: ['billing:manage'],
        },
        accessToken: 'tenant-a-token',
        refreshToken: 'tenant-a-refresh',
        tenantId: 'tenant-b',
        accessTokenExpires: Date.now(),
      };

      const result = await refreshAccessToken(token, mockConfig);

      expect(result.user.roles).toEqual(['Member']);
      expect(result.user.permissions).toEqual(['support:read']);
    });
  });
});
