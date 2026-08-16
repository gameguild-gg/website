/**
 * Google Provider Tests
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { GoogleProvider } from '../../../src/runtime/auth/providers/google.js';
import { OAuthError } from '../../../src/runtime/auth/errors.js';

describe('GoogleProvider', () => {
  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('should create provider with correct config', () => {
    const provider = GoogleProvider({
      clientId: 'google-client-id',
      clientSecret: 'google-client-secret',
    });

    expect(provider.id).toBe('google');
    expect(provider.name).toBe('Google');
    expect(provider.type).toBe('oidc');
    expect(provider.clientId).toBe('google-client-id');
    expect(provider.clientSecret).toBe('google-client-secret');
    expect(provider.exchangeToken).toBeDefined();
  });

  it('should have correct default authorization URLs', () => {
    const provider = GoogleProvider({
      clientId: 'id',
      clientSecret: 'secret',
    });

    expect(provider.authorization.url).toBe('https://accounts.google.com/o/oauth2/v2/auth');
    expect(provider.authorization.params.scope).toBe('openid email profile');
    expect(provider.token.url).toBe('https://oauth2.googleapis.com/token');
    expect(provider.userinfo.url).toBe('https://www.googleapis.com/oauth2/v3/userinfo');
  });

  describe('exchangeToken', () => {
    it('should exchange ID token for backend tokens', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'google-access',
          refreshToken: 'google-refresh',
          expiresIn: 3600,
          userId: 'user-1',
          email: 'test@gmail.com',
          user: {
            displayName: 'Google User',
            profilePictureUrl: 'https://lh3.google.com/pic.jpg',
          },
        }),
      });
      globalThis.fetch = mockFetch;

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      const result = await provider.exchangeToken('google-id-token', 'http://localhost:5295');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5295/v1/auth/google:sign-in',
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ idToken: 'google-id-token' }),
        }),
      );

      expect(result.tokens.accessToken).toBe('google-access');
      expect(result.tokens.refreshToken).toBe('google-refresh');
      expect(result.user.id).toBe('user-1');
      expect(result.user.email).toBe('test@gmail.com');
      expect(result.user.name).toBe('Google User');
      expect(result.user.image).toBe('https://lh3.google.com/pic.jpg');
    });

    it('should include tenantId when provided', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'token',
          userId: 'user-1',
        }),
      });
      globalThis.fetch = mockFetch;

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await provider.exchangeToken('id-token', 'http://localhost:5295', 'tenant-1');

      const body = JSON.parse(mockFetch.mock.calls[0][1].body);
      expect(body.tenantId).toBe('tenant-1');
    });

    it('should use custom tokenExchangePath', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'token',
          userId: 'user-1',
        }),
      });
      globalThis.fetch = mockFetch;

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
        tokenExchangePath: '/custom/google/exchange',
      });

      await provider.exchangeToken('id-token', 'http://localhost:5295');

      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('/custom/google/exchange');
    });

    it('should prefer options.apiUrl over passed apiUrl', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ accessToken: 'token', userId: 'user-1' }),
      });
      globalThis.fetch = mockFetch;

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
        apiUrl: 'http://options-api:5295',
      });

      await provider.exchangeToken('id-token', 'http://passed-api:5295');

      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('http://options-api:5295');
    });

    it('should throw OAuthError on non-ok response', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({ message: 'Invalid Google token' }),
      });

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await expect(provider.exchangeToken('bad-token', 'http://localhost:5295')).rejects.toThrow(OAuthError);
    });

    it('should throw OAuthError with default message on non-JSON error', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('not json');
        },
      });

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await expect(provider.exchangeToken('id-token', 'http://localhost:5295')).rejects.toThrow('Google sign-in failed');
    });

    it('should handle response without user object', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'token',
          refreshToken: 'refresh',
          userId: 'user-1',
          email: 'test@gmail.com',
        }),
      });
      globalThis.fetch = mockFetch;

      const provider = GoogleProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      const result = await provider.exchangeToken('id-token', 'http://localhost:5295');

      expect(result.user.id).toBe('user-1');
      expect(result.user.email).toBe('test@gmail.com');
      expect(result.user.name).toBeNull();
      expect(result.user.image).toBeNull();
    });
  });
});
