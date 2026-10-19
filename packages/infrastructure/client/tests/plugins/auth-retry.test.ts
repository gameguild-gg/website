/**
 * Tests for Auth Retry Plugin
 */
import { describe, it, expect, vi } from 'vitest';
import { createAuthRetryPlugin } from '../../src/plugins/auth-retry.js';
import { ok, err } from '../../src/runtime/result/helpers.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { Transport, RequestConfig, ApiResponse } from '../../src/runtime/transport/types.js';
import type { Result } from '../../src/runtime/result/types.js';

function make401Error(): ApiError {
  return {
    name: 'ApiError',
    status: 401,
    code: 'AUTHENTICATION_ERROR',
    message: 'Unauthorized',
  };
}

function make403Error(): ApiError {
  return {
    name: 'ApiError',
    status: 403,
    code: 'FORBIDDEN',
    message: 'Forbidden',
  };
}

function makeSuccessResponse<T>(data: T): Result<ApiResponse<T>, ApiError> {
  return ok({ data, status: 200, headers: new Headers() });
}

function makeMockTransport(
  responses: Array<Result<ApiResponse<unknown>, ApiError>>
): Transport {
  let callIndex = 0;
  return {
    async request<T>(_config: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
      const response = responses[callIndex++];
      return response as Result<ApiResponse<T>, ApiError>;
    },
  };
}

describe('Auth Retry Plugin', () => {
  it('passes through successful requests without retrying', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const plugin = createAuthRetryPlugin({ refreshToken });

    const transport = makeMockTransport([makeSuccessResponse({ id: 1 })]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request<{ id: number }>({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(true);
    expect(refreshToken).not.toHaveBeenCalled();
  });

  it('retries on 401 after successful token refresh', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const plugin = createAuthRetryPlugin({ refreshToken });

    const transport = makeMockTransport([
      err(make401Error()),        // First call: 401
      makeSuccessResponse({ id: 1 }), // After refresh: success
    ]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request<{ id: number }>({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(true);
    expect(refreshToken).toHaveBeenCalledTimes(1);
  });

  it('calls onAuthenticationRequired when refresh fails', async () => {
    const refreshToken = vi.fn().mockResolvedValue(false);
    const onAuthRequired = vi.fn();
    const plugin = createAuthRetryPlugin({
      refreshToken,
      onAuthenticationRequired: onAuthRequired,
    });

    const transport = makeMockTransport([err(make401Error())]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(refreshToken).toHaveBeenCalledTimes(1);
    expect(onAuthRequired).toHaveBeenCalledTimes(1);
  });

  it('does not retry non-401 errors', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const plugin = createAuthRetryPlugin({ refreshToken });

    const transport = makeMockTransport([err(make403Error())]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(refreshToken).not.toHaveBeenCalled();
  });

  it('respects shouldRetryOnUnauthorized filter', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const plugin = createAuthRetryPlugin({
      refreshToken,
      shouldRetryOnUnauthorized: () => false, // never retry
    });

    const transport = makeMockTransport([err(make401Error())]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(refreshToken).not.toHaveBeenCalled();
  });

  it('respects maxRetries configuration', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const plugin = createAuthRetryPlugin({ refreshToken, maxRetries: 2 });

    // 3 consecutive 401s: should try refresh twice, then give up
    const transport = makeMockTransport([
      err(make401Error()),
      err(make401Error()),
      err(make401Error()),
    ]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(refreshToken).toHaveBeenCalledTimes(2);
    if (!result.ok) {
      expect(result.error.metadata?.authRetryExhausted).toBe(true);
    }
  });

  it('calls onAuthenticationRequired when retries exhausted', async () => {
    const refreshToken = vi.fn().mockResolvedValue(true);
    const onAuthRequired = vi.fn();
    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 1,
      onAuthenticationRequired: onAuthRequired,
    });

    const transport = makeMockTransport([
      err(make401Error()),
      err(make401Error()),
    ]);
    const wrapped = plugin.wrapTransport(transport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(onAuthRequired).toHaveBeenCalledTimes(1);
  });
});
