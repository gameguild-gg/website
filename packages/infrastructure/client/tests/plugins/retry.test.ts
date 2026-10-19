/**
 * Extended Retry Plugin Tests
 *
 * Tests for createRetryPlugin wrapTransport behavior,
 * exponential backoff, and shouldRetry.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createRetryPlugin, createRetryInterceptor } from '../../src/plugins/retry.js';
import type { Transport, RequestConfig } from '../../src/runtime/transport/types.js';
import { ok, err } from '../../src/runtime/result/helpers.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import { legacyCodeError } from '../support/contract-fixtures.js';

describe('createRetryPlugin', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should return immediately on success without retrying', async () => {
    const mockTransport: Transport = {
      request: vi.fn().mockResolvedValue(
        ok({ data: { success: true }, status: 200, headers: new Headers() }),
      ),
    };

    const plugin = createRetryPlugin({ maxRetries: 3, baseDelay: 100 });
    const wrapped = plugin.wrapTransport(mockTransport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(true);
    expect(mockTransport.request).toHaveBeenCalledTimes(1);
  });

  it('should retry on retryable errors', async () => {
    const retryableError: ApiError = legacyCodeError({
      name: 'ApiError',
      message: 'Service unavailable',
      status: 503,
      code: 'SERVICE_UNAVAILABLE',
    });

    const mockTransport: Transport = {
      request: vi.fn()
        .mockResolvedValueOnce(err(retryableError))
        .mockResolvedValueOnce(err(retryableError))
        .mockResolvedValueOnce(
          ok({ data: { recovered: true }, status: 200, headers: new Headers() }),
        ),
    };

    const plugin = createRetryPlugin({
      maxRetries: 3,
      baseDelay: 10,
      maxDelay: 100,
    });
    const wrapped = plugin.wrapTransport(mockTransport);

    const resultPromise = wrapped.request({ method: 'GET', path: '/test' });

    // Advance timers to handle delays
    await vi.runAllTimersAsync();

    const result = await resultPromise;

    expect(result.ok).toBe(true);
    expect(mockTransport.request).toHaveBeenCalledTimes(3);
  });

  it('should not retry non-retryable errors', async () => {
    const nonRetryableError: ApiError = {
      name: 'ApiError',
      message: 'Bad request',
      status: 400,
      code: 'VALIDATION_ERROR',
    };

    const mockTransport: Transport = {
      request: vi.fn().mockResolvedValue(err(nonRetryableError)),
    };

    const plugin = createRetryPlugin({ maxRetries: 3, baseDelay: 10 });
    const wrapped = plugin.wrapTransport(mockTransport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(mockTransport.request).toHaveBeenCalledTimes(1);
  });

  it('should exhaust retries and return error with metadata', async () => {
    const retryableError: ApiError = {
      name: 'ApiError',
      message: 'Timeout',
      status: 504,
      code: 'TIMEOUT',
    };

    const mockTransport: Transport = {
      request: vi.fn().mockResolvedValue(err(retryableError)),
    };

    const plugin = createRetryPlugin({ maxRetries: 2, baseDelay: 10, maxDelay: 100 });
    const wrapped = plugin.wrapTransport(mockTransport);

    const resultPromise = wrapped.request({ method: 'GET', path: '/test' });

    await vi.runAllTimersAsync();

    const result = await resultPromise;

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.retriesExhausted).toBe(true);
      expect(result.error.metadata?.totalAttempts).toBe(3);
    }
    // Initial + 2 retries = 3 calls
    expect(mockTransport.request).toHaveBeenCalledTimes(3);
  });

  it('should use custom shouldRetry function', async () => {
    const customError: ApiError = legacyCodeError({
      name: 'ApiError',
      message: 'Custom error',
      status: 418,
      code: 'CUSTOM',
    });

    const mockTransport: Transport = {
      request: vi.fn().mockResolvedValue(err(customError)),
    };

    const shouldRetry = vi.fn().mockReturnValue(false);
    const plugin = createRetryPlugin({
      maxRetries: 3,
      baseDelay: 10,
      shouldRetry,
    });
    const wrapped = plugin.wrapTransport(mockTransport);

    const result = await wrapped.request({ method: 'GET', path: '/test' });

    expect(result.ok).toBe(false);
    expect(shouldRetry).toHaveBeenCalledWith(customError);
    expect(mockTransport.request).toHaveBeenCalledTimes(1);
  });

  it('should use linear delay when exponentialBackoff is false', async () => {
    const retryableError: ApiError = legacyCodeError({
      name: 'ApiError',
      message: 'Unavailable',
      status: 503,
      code: 'SERVICE_UNAVAILABLE',
    });

    const mockTransport: Transport = {
      request: vi.fn().mockResolvedValue(err(retryableError)),
    };

    const plugin = createRetryPlugin({
      maxRetries: 1,
      baseDelay: 100,
      exponentialBackoff: false,
    });
    const wrapped = plugin.wrapTransport(mockTransport);

    const resultPromise = wrapped.request({ method: 'GET', path: '/test' });

    await vi.runAllTimersAsync();

    const result = await resultPromise;

    expect(result.ok).toBe(false);
    expect(mockTransport.request).toHaveBeenCalledTimes(2);
  });
});

describe('createRetryInterceptor (deprecated)', () => {
  it('should create an interceptor with onError handler', () => {
    const interceptor = createRetryInterceptor({ maxRetries: 3 });

    expect(interceptor.onError).toBeDefined();
  });

  it('should not mark non-retryable errors for retry', async () => {
    const interceptor = createRetryInterceptor({ maxRetries: 3 });

    const error: ApiError = {
      name: 'ApiError',
      message: 'Bad request',
      status: 400,
      code: 'VALIDATION_ERROR',
    };

    const result = await interceptor.onError!(error);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.shouldRetry).toBeUndefined();
    }
  });

  it('should mark retryable errors with retry metadata', async () => {
    const interceptor = createRetryInterceptor({ maxRetries: 3 });

    const error: ApiError = legacyCodeError({
      name: 'ApiError',
      message: 'Unavailable',
      status: 503,
      code: 'SERVICE_UNAVAILABLE',
    });

    const result = await interceptor.onError!(error);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.shouldRetry).toBe(true);
      expect(result.error.metadata?.retryAttempt).toBe(1);
    }
  });

  it('should mark retries exhausted when max reached', async () => {
    const interceptor = createRetryInterceptor({ maxRetries: 2 });

    const error: ApiError = legacyCodeError({
      name: 'ApiError',
      message: 'Unavailable',
      status: 503,
      code: 'SERVICE_UNAVAILABLE',
      metadata: { retryAttempt: 2 },
    });

    const result = await interceptor.onError!(error);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.retriesExhausted).toBe(true);
    }
  });
});
