/**
 * Token refresh tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TokenRefreshManager } from '../../src/runtime/auth/refresh.js';
import type { TokenProvider } from '../../src/runtime/auth/types.js';

const fetchMock = vi.fn<typeof fetch>();
global.fetch = fetchMock;

describe('TokenRefreshManager', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should use configurable refresh endpoint', async () => {
    const customEndpoint = '/custom/token/refresh';

    fetchMock.mockResolvedValue(new Response(JSON.stringify({
      accessToken: 'new-access-token',
      refreshToken: 'new-refresh-token',
      expiresIn: 3600,
    })));

    const provider: TokenProvider = {
      getAccessToken: async () => 'current-access-token',
      getRefreshToken: async () => 'current-refresh-token',
      onTokenRefresh: vi.fn(),
    };

    const manager = new TokenRefreshManager(provider, 'http://localhost:5000', {
      refreshUrl: customEndpoint,
    });

    manager.setExpiry(1); // Expire in 1 second
    await manager.refresh();

    expect(global.fetch).toHaveBeenCalledWith(
      `http://localhost:5000${customEndpoint}`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ refreshToken: 'current-refresh-token' }),
      }),
    );
  });

  it('should refresh token when needed', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({
      accessToken: 'new-token',
      refreshToken: 'new-refresh',
      expiresIn: 3600,
    })));

    const provider: TokenProvider = {
      getAccessToken: async () => 'current-access-token',
      getRefreshToken: async () => 'refresh-token',
      onTokenRefresh: vi.fn(),
    };

    const manager = new TokenRefreshManager(provider, 'http://localhost:5000');

    manager.setExpiry(20); // Expire in 20 seconds
    expect(manager.shouldRefresh()).toBe(true);

    const result = await manager.refreshIfNeeded();

    expect(result).not.toBeNull();
    expect(result?.accessToken).toBe('new-token');
    expect(provider.onTokenRefresh).toHaveBeenCalled();
  });

  it('should not refresh if not needed', async () => {
    const provider: TokenProvider = {
      getAccessToken: async () => 'current-access-token',
      getRefreshToken: async () => 'refresh-token',
    };

    const manager = new TokenRefreshManager(provider, 'http://localhost:5000');

    manager.setExpiry(60); // Expire in 60 seconds (more than threshold)
    expect(manager.shouldRefresh()).toBe(false);

    const result = await manager.refreshIfNeeded();

    expect(result).toBeNull();
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('should handle refresh failures with retries', async () => {
    let attempts = 0;
    fetchMock.mockImplementation(async () => {
      attempts++;
      if (attempts < 3) {
        return new Response(null, { status: 500, statusText: 'Server Error' });
      }
      return new Response(JSON.stringify({
        accessToken: 'new-token',
        refreshToken: 'new-refresh',
        expiresIn: 3600,
      }));
    });

    const provider: TokenProvider = {
      getAccessToken: async () => 'current-access-token',
      getRefreshToken: async () => 'refresh-token',
      onTokenRefresh: vi.fn(),
    };

    const manager = new TokenRefreshManager(provider, 'http://localhost:5000', {
      maxRetries: 3,
      backoffBase: 10, // Small delay for tests
    });

    const result = await manager.refresh();

    expect(result).not.toBeNull();
    expect(attempts).toBe(3);
  });

  it('should prevent concurrent refresh calls (mutex)', async () => {
    let refreshCalls = 0;

    fetchMock.mockImplementation(async () => {
      refreshCalls++;
      // Simulate slow network
      await new Promise((resolve) => setTimeout(resolve, 100));
      return new Response(JSON.stringify({
        accessToken: 'new-token',
        refreshToken: 'new-refresh',
        expiresIn: 3600,
      }));
    });

    const provider: TokenProvider = {
      getAccessToken: async () => 'current-access-token',
      getRefreshToken: async () => 'refresh-token',
      onTokenRefresh: vi.fn(),
    };

    const manager = new TokenRefreshManager(provider, 'http://localhost:5000');

    // Trigger multiple concurrent refreshes
    const results = await Promise.all([manager.refresh(), manager.refresh(), manager.refresh()]);

    // Should only make one actual refresh call due to mutex
    expect(refreshCalls).toBe(1);
    expect(results).toHaveLength(3);
    expect(results.every((r) => r?.accessToken === 'new-token')).toBe(true);
  });
});
