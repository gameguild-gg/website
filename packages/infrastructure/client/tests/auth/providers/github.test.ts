/**
 * GitHub Provider Tests
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GitHubProvider } from '../../../src/runtime/auth/providers/github.js';
import { OAuthError } from '../../../src/runtime/auth/errors.js';

describe('GitHubProvider', () => {
  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('creates the provider with GitHub defaults', () => {
    const provider = GitHubProvider({ clientId: 'gh-client-id', clientSecret: 'gh-client-secret' });

    expect(provider).toMatchObject({
      id: 'github',
      name: 'GitHub',
      type: 'oauth',
      clientId: 'gh-client-id',
      clientSecret: 'gh-client-secret',
      authorization: {
        url: 'https://github.com/login/oauth/authorize',
        params: { scope: 'read:user user:email' },
      },
      token: { url: 'https://github.com/login/oauth/access_token' },
      userinfo: { url: 'https://api.github.com/user' },
    });
  });

  describe('getAuthorizeUrl', () => {
    it('requests the backend authorize endpoint and includes redirectUri', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ authUrl: 'https://github.com/login?state=abc' }),
      });
      globalThis.fetch = mockFetch;

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });
      const url = await provider.getAuthorizeUrl('http://localhost:8080', 'http://localhost:3000/callback');

      expect(mockFetch).toHaveBeenCalledWith(expect.stringMatching(/^http:\/\/localhost:8080\/v1\/auth\/github:authorize\?redirectUri=/), { method: 'GET', redirect: 'error' });
      expect(url).toBe('https://github.com/login?state=abc');
    });

    it('uses custom paths and the configured API URL', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ authUrl: 'https://github.com/login' }),
      });
      globalThis.fetch = mockFetch;

      const provider = GitHubProvider({
        clientId: 'id',
        clientSecret: 'secret',
        apiUrl: 'http://configured-api:8080',
        authorizePath: '/custom/github/auth',
      });

      await provider.getAuthorizeUrl('http://ignored-api:8080');

      expect(mockFetch).toHaveBeenCalledWith('http://configured-api:8080/custom/github/auth?', { method: 'GET', redirect: 'error' });
    });

    it('preserves structured backend error details', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({ detail: 'GitHub authentication is temporarily unavailable.' }),
      });

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });

      await expect(provider.getAuthorizeUrl('http://localhost:8080')).rejects.toThrow('GitHub authentication is temporarily unavailable.');
    });
  });

  describe('handleCallback', () => {
    it('maps tokens, claims, session, tenant, and user metadata', async () => {
      const responseData = {
        accessToken: 'gh-access-token',
        refreshToken: 'gh-refresh-token',
        expiresIn: 3600,
        accessTokenExpiresAt: '2030-01-01T00:00:00Z',
        refreshTokenExpiresAt: '2030-02-01T00:00:00Z',
        userId: 'user-1',
        email: 'test@example.com',
        roles: ['admin'],
        permissions: ['courses:write'],
        sessionId: 'session-1',
        tenantId: 'tenant-1',
        availableTenants: [{ id: 'tenant-1', name: 'Primary' }],
        user: { displayName: 'GitHub User', profilePictureUrl: 'https://pic.com/a.png' },
      };
      const mockFetch = vi.fn().mockResolvedValue({ ok: true, json: async () => responseData });
      globalThis.fetch = mockFetch;

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });
      const result = await provider.handleCallback('http://localhost:8080', 'auth-code-123', 'state-value');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/v1/auth/github:callback',
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: 'auth-code-123', state: 'state-value' }),
        }),
      );
      expect(result).toEqual({
        tokens: {
          accessToken: 'gh-access-token',
          refreshToken: 'gh-refresh-token',
          expiresIn: 3600,
          accessTokenExpiresAt: '2030-01-01T00:00:00Z',
          refreshTokenExpiresAt: '2030-02-01T00:00:00Z',
          tokenType: 'Bearer',
        },
        user: {
          id: 'user-1',
          email: 'test@example.com',
          name: 'GitHub User',
          image: 'https://pic.com/a.png',
          roles: ['admin'],
          permissions: ['courses:write'],
        },
        sessionId: 'session-1',
        tenantId: 'tenant-1',
        availableTenants: [{ id: 'tenant-1', name: 'Primary' }],
      });
    });

    it('uses backend user values when top-level identity is absent', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'token',
          user: { id: 'nested-id', email: 'nested@example.com', role: 'operator', scope: 'users:read' },
        }),
      });

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });
      const result = await provider.handleCallback('http://localhost:8080', 'code');

      expect(result.user).toMatchObject({
        id: 'nested-id',
        email: 'nested@example.com',
        roles: ['operator'],
        permissions: ['users:read'],
      });
    });

    it('preserves structured callback errors', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({ message: 'Invalid authorization code.' }),
      });

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });

      await expect(provider.handleCallback('http://localhost:8080', 'bad-code')).rejects.toThrow('Invalid authorization code.');
    });

    it('uses the fallback message for non-JSON callback errors', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => {
          throw new Error('not json');
        },
      });

      const provider = GitHubProvider({ clientId: 'id', clientSecret: 'secret' });

      await expect(provider.handleCallback('http://localhost:8080', 'code')).rejects.toThrow(new OAuthError('GitHub sign-in failed'));
    });
  });
});
