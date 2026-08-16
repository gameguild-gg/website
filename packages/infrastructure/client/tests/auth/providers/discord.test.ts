/**
 * Discord Provider Tests
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { DiscordProvider } from '../../../src/runtime/auth/providers/discord.js';
import { OAuthError } from '../../../src/runtime/auth/errors.js';

describe('DiscordProvider', () => {
  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('should create provider with correct config', () => {
    const provider = DiscordProvider({
      clientId: 'dc-client-id',
      clientSecret: 'dc-client-secret',
    });

    expect(provider.id).toBe('discord');
    expect(provider.name).toBe('Discord');
    expect(provider.type).toBe('oauth');
    expect(provider.clientId).toBe('dc-client-id');
    expect(provider.clientSecret).toBe('dc-client-secret');
    expect(provider.getAuthorizeUrl).toBeDefined();
    expect(provider.handleCallback).toBeDefined();
  });

  it('should have correct default authorization URLs', () => {
    const provider = DiscordProvider({
      clientId: 'id',
      clientSecret: 'secret',
    });

    expect(provider.authorization.url).toBe(
      'https://discord.com/oauth2/authorize',
    );
    expect(provider.authorization.params.scope).toBe('identify email');
    expect(provider.token.url).toBe('https://discord.com/api/oauth2/token');
    expect(provider.userinfo.url).toBe(
      'https://discord.com/api/v10/users/@me',
    );
  });

  describe('getAuthorizeUrl', () => {
    it('should POST redirectUri as JSON to backend authorize endpoint', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ authUrl: 'https://discord.com/oauth2/authorize?state=abc' }),
      });
      globalThis.fetch = mockFetch;

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      const url = await provider.getAuthorizeUrl(
        'http://localhost:8080',
        'http://localhost:3000/api/auth/callback/discord',
      );

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/v1/auth/discord:sign-in-authorize',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            redirectUri: 'http://localhost:3000/api/auth/callback/discord',
          }),
        },
      );
      expect(url).toBe('https://discord.com/oauth2/authorize?state=abc');
    });

    it('should use custom authorizePath', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ authUrl: 'https://discord.com/oauth2/authorize' }),
      });
      globalThis.fetch = mockFetch;

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
        authorizePath: '/custom/discord/auth',
      });

      await provider.getAuthorizeUrl('http://localhost:8080');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/custom/discord/auth'),
        expect.anything(),
      );
    });

    it('should prefer options.apiUrl over passed apiUrl', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ authUrl: 'https://discord.com/oauth2/authorize' }),
      });
      globalThis.fetch = mockFetch;

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
        apiUrl: 'http://options-api:5295',
      });

      await provider.getAuthorizeUrl('http://passed-api:5295');

      const calledUrl = mockFetch.mock.calls[0][0] as string;
      expect(calledUrl).toContain('http://options-api:5295');
    });

    it('should throw OAuthError with parsed message on non-ok response', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({ message: 'Discord OAuth is not configured' }),
      });

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await expect(
        provider.getAuthorizeUrl('http://localhost:8080'),
      ).rejects.toThrow('Discord OAuth is not configured');
    });
  });

  describe('handleCallback', () => {
    it('should POST full body and parse ProviderResult', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          accessToken: 'dc-access-token',
          refreshToken: 'dc-refresh-token',
          userId: 'user-1',
          email: 'test@example.com',
          user: { displayName: 'Discord User', profilePictureUrl: 'https://cdn.discordapp.com/avatars/1/abc.png' },
          tenantId: 'tenant-9',
        }),
      });
      globalThis.fetch = mockFetch;

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      const result = await provider.handleCallback(
        'http://localhost:8080',
        'auth-code-123',
        'state-value',
        'http://localhost:3000/api/auth/callback/discord',
        'tenant-9',
      );

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/v1/auth/discord:sign-in-callback',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            code: 'auth-code-123',
            state: 'state-value',
            redirectUri: 'http://localhost:3000/api/auth/callback/discord',
            tenantId: 'tenant-9',
          }),
        }),
      );

      expect(result.tokens.accessToken).toBe('dc-access-token');
      expect(result.tokens.refreshToken).toBe('dc-refresh-token');
      expect(result.tokens.tokenType).toBe('Bearer');
      expect(result.user.id).toBe('user-1');
      expect(result.user.email).toBe('test@example.com');
      expect(result.user.name).toBe('Discord User');
      expect(result.user.image).toBe('https://cdn.discordapp.com/avatars/1/abc.png');
      expect(result.tenantId).toBe('tenant-9');
    });

    it('should throw OAuthError with parsed message on non-ok response', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({ message: 'Invalid code' }),
      });

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await expect(
        provider.handleCallback('http://localhost:8080', 'bad-code'),
      ).rejects.toThrow('Invalid code');
    });

    it('should handle non-JSON error response', async () => {
      globalThis.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => { throw new Error('not json'); },
      });

      const provider = DiscordProvider({
        clientId: 'id',
        clientSecret: 'secret',
      });

      await expect(
        provider.handleCallback('http://localhost:8080', 'code'),
      ).rejects.toThrow(OAuthError);
    });
  });
});
