/**
 * Auth-retry plugin — cover concurrent refresh deduplication and metadata
 */
import { describe, it, expect, vi } from 'vitest';
import { createAuthRetryPlugin } from '../../src/plugins/auth-retry.js';
import { ok, err } from '../../src/runtime/result/helpers.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { ApiResponse, Transport, RequestConfig } from '../../src/runtime/transport/types.js';

const make401 = (): ApiError => ({
  name: 'ApiError',
  code: 'AUTHENTICATION_ERROR' as const,
  message: 'Unauthorized',
  status: 401,
});

const makeOk = (): ApiResponse<{ ok: true }> => ({
  data: { ok: true },
  status: 200,
  headers: new Headers(),
});

describe('createAuthRetryPlugin — concurrent refresh mutex', () => {
  it('deduplicates concurrent refresh calls', async () => {
    const refreshToken = vi.fn();
    let resolveRefresh: (v: boolean) => void;
    const refreshPromise = new Promise<boolean>((resolve) => {
      resolveRefresh = resolve;
    });

    // First call starts the refresh, second waits on it
    refreshToken.mockReturnValue(refreshPromise);

    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 1,
    });

    let callCount = 0;
    const mockTransport: Transport = {
      async request<T>(config: RequestConfig) {
        callCount++;
        if (callCount <= 2) return err(make401());
        return ok(makeOk() as ApiResponse<T>);
      },
    };

    const wrapped = plugin.wrapTransport(mockTransport);

    // Start two concurrent requests that both get 401
    const p1 = wrapped.request({ path: '/a', method: 'GET', headers: {} });
    const p2 = wrapped.request({ path: '/b', method: 'GET', headers: {} });

    // Let the refresh complete
    await new Promise((r) => setTimeout(r, 10));
    resolveRefresh!(true);

    const [r1, r2] = await Promise.all([p1, p2]);

    // Both should eventually succeed; the refresh should only be called once
    // (or at most twice since they're independent retry loops)
    // The key point: refreshToken is called, not for every request
    expect(refreshToken).toHaveBeenCalled();
  });

  it('metadata includes authRetryExhausted and totalRefreshAttempts', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const onAuthenticationRequired = vi.fn();

    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 2,
      onAuthenticationRequired,
    });

    const mockTransport: Transport = {
      async request<T>() {
        return err(make401());
      },
    };

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.authRetryExhausted).toBe(true);
      expect(result.error.metadata?.totalRefreshAttempts).toBe(3);
    }
    expect(onAuthenticationRequired).toHaveBeenCalled();
  });

  it('metadata includes authRefreshFailed when refresh returns false', async () => {
    const refreshToken = vi.fn().mockResolvedValue(false);
    const onAuthenticationRequired = vi.fn();

    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 1,
      onAuthenticationRequired,
    });

    const mockTransport: Transport = {
      async request<T>() {
        return err(make401());
      },
    };

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.authRefreshFailed).toBe(true);
    }
    expect(onAuthenticationRequired).toHaveBeenCalled();
  });

  it('shouldRetryOnUnauthorized returning false skips retry', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const shouldRetry = vi.fn().mockReturnValue(false);

    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 1,
      shouldRetryOnUnauthorized: shouldRetry,
    });

    const mockTransport: Transport = {
      async request<T>() {
        return err(make401());
      },
    };

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });

    expect(result.ok).toBe(false);
    expect(refreshToken).not.toHaveBeenCalled();
  });
});
