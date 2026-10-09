/**
 * TokenRefreshManager — cover retry backoff and all-retries-failed paths
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { TokenRefreshManager } from '../../src/runtime/auth/refresh.js';

const mockFetch = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch);
  mockFetch.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('TokenRefreshManager — full branch coverage', () => {
  it.each([401, 403])('requests authentication once without retrying a denied %s refresh', async (status) => {
    const onAuthenticationRequired = vi.fn();
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const manager = new TokenRefreshManager({
      getAccessToken: async () => 'synthetic-access',
      getRefreshToken: async () => 'synthetic-refresh',
      onAuthenticationRequired,
    }, 'http://localhost', { maxRetries: 3, backoffBase: 0 });
    mockFetch.mockResolvedValue({ ok: false, status, statusText: 'Denied' });
    try {
      expect(await manager.refresh()).toBeNull();
      expect(mockFetch).toHaveBeenCalledTimes(1);
      expect(onAuthenticationRequired).toHaveBeenCalledTimes(1);
    } finally {
      errorSpy.mockRestore();
    }
  });

  it('shouldRefresh returns false when no expiry set', () => {
    const manager = new TokenRefreshManager({ getAccessToken: async () => 'tok' }, 'http://localhost');
    expect(manager.shouldRefresh()).toBe(false);
  });

  it('shouldRefresh returns false when token is not near expiry', () => {
    const manager = new TokenRefreshManager({ getAccessToken: async () => 'tok' }, 'http://localhost');
    manager.setExpiry(3600); // 1 hour from now
    expect(manager.shouldRefresh()).toBe(false);
  });

  it('shouldRefresh returns true when token is near expiry', () => {
    const manager = new TokenRefreshManager({ getAccessToken: async () => 'tok' }, 'http://localhost', { refreshThreshold: 60_000 });
    manager.setExpiry(30); // 30 seconds from now, threshold is 60s
    expect(manager.shouldRefresh()).toBe(true);
  });

  it('refreshIfNeeded returns null when no refresh needed', async () => {
    const manager = new TokenRefreshManager({ getAccessToken: async () => 'tok' }, 'http://localhost');
    const result = await manager.refreshIfNeeded();
    expect(result).toBeNull();
  });

  it('successful refresh updates tokens via provider callback', async () => {
    const onTokenRefresh = vi.fn();
    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'old-at',
        getRefreshToken: async () => 'old-rt',
        onTokenRefresh,
      },
      'http://localhost',
      { maxRetries: 1 },
    );

    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        expiresIn: 3600,
      }),
    });

    const tokens = await manager.refresh();

    expect(tokens).toBeTruthy();
    expect(tokens!.accessToken).toBe('new-at');
    expect(onTokenRefresh).toHaveBeenCalledWith(tokens);
  });

  it('no refresh token triggers onAuthenticationRequired', async () => {
    const onAuthRequired = vi.fn();
    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        // getRefreshToken not provided → undefined
        onAuthenticationRequired: onAuthRequired,
      },
      'http://localhost',
    );

    const result = await manager.refresh();
    expect(result).toBeNull();
    expect(onAuthRequired).toHaveBeenCalled();
  });

  it('retry with exponential backoff on failure', async () => {
    vi.useFakeTimers();
    const onAuthRequired = vi.fn();
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        getRefreshToken: async () => 'rt',
        onAuthenticationRequired: onAuthRequired,
      },
      'http://localhost',
      { maxRetries: 2, backoffBase: 100 },
    );

    // Both attempts receive a temporary service failure.
    mockFetch.mockResolvedValue({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
    });

    const refreshPromise = manager.refresh();

    // First attempt fails immediately, then backoff of 100ms
    await vi.advanceTimersByTimeAsync(150);
    // Second attempt fails, no more retries

    await vi.advanceTimersByTimeAsync(500);
    const result = await refreshPromise;

    expect(result).toBeNull();
    expect(onAuthRequired).toHaveBeenCalled();
    expect(errorSpy).toHaveBeenCalledWith('Token refresh failed after', 2, 'attempts:', expect.any(Error));
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('mutex pattern reuses existing refresh promise', async () => {
    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        getRefreshToken: async () => 'rt',
      },
      'http://localhost',
      { maxRetries: 1 },
    );

    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        expiresIn: 3600,
      }),
    });

    // Start two concurrent refreshes
    const [r1, r2] = await Promise.all([manager.refresh(), manager.refresh()]);

    // Both should return same result; fetch called only once
    expect(mockFetch).toHaveBeenCalledTimes(1);
    expect(r1).toEqual(r2);
  });

  it('non-Error throw in executeRefresh is wrapped', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const onAuthRequired = vi.fn();

    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        getRefreshToken: async () => 'rt',
        onAuthenticationRequired: onAuthRequired,
      },
      'http://localhost',
      { maxRetries: 1, backoffBase: 0 },
    );

    // Make fetch throw a non-Error value
    mockFetch.mockRejectedValue('string error');

    const result = await manager.refresh();
    expect(result).toBeNull();
    expect(onAuthRequired).toHaveBeenCalled();
  });

  it('successful refresh sets expiry from expiresIn', async () => {
    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        getRefreshToken: async () => 'rt',
      },
      'http://localhost',
      { maxRetries: 1 },
    );

    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        expiresIn: 3600,
      }),
    });

    await manager.refresh();

    // After refresh, shouldRefresh should be false (1 hour until expiry)
    expect(manager.shouldRefresh()).toBe(false);
  });

  it('refresh without expiresIn does not set expiry', async () => {
    const manager = new TokenRefreshManager(
      {
        getAccessToken: async () => 'tok',
        getRefreshToken: async () => 'rt',
      },
      'http://localhost',
      { maxRetries: 1 },
    );

    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        // no expiresIn
      }),
    });

    await manager.refresh();
    // shouldRefresh returns false since no expiry was set
    expect(manager.shouldRefresh()).toBe(false);
  });
});
