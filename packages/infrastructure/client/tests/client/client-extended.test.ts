/**
 * Extended Client Tests — deduplication, devtools, token refresh, tenant
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createClient } from '../../src/client.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { Result } from '../../src/runtime/result/types.js';
import type { ApiResponse, Interceptor, RequestConfig } from '../../src/runtime/transport/types.js';

type HeaderProvider = Parameters<typeof import('../../src/runtime/transport/fetch.js').createHeaderInterceptor>[0];
type FixtureBody = { message: string; path: string };

// Mock the DevTools — must be a class so it works with `new DevTools()`
vi.mock('../../src/runtime/devtools/index.js', () => ({
  DevTools: class MockDevTools {
    logRequestStart = vi.fn();
    logRequestComplete = vi.fn();
    logDeduplication = vi.fn();
    logCacheHit = vi.fn();
    logCacheMiss = vi.fn();
    logRetry = vi.fn();
    logValidationError = vi.fn();
  },
}));

// Mock TokenRefreshManager — must be a class so it works with `new`
vi.mock('../../src/runtime/auth/refresh.js', () => ({
  TokenRefreshManager: class MockTokenRefreshManager {
    refreshIfNeeded = vi.fn(async () => {});
  },
}));

// Mock the transport
vi.mock('../../src/runtime/transport/fetch.js', () => ({
  createFetchTransport: vi.fn(() => ({
    request: vi.fn(async (reqConfig: RequestConfig): Promise<Result<ApiResponse<FixtureBody>, ApiError>> => ({
      ok: true,
      data: {
        data: { message: 'success', path: reqConfig.path },
        status: 200,
        headers: new Headers(),
      },
    })),
  })),
  createHeaderInterceptor: vi.fn((getHeaders: HeaderProvider): Interceptor => ({
    onRequest: vi.fn(async (config: RequestConfig) => {
      const headers = await getHeaders();
      return { ...config, headers: { ...config.headers, ...headers } };
    }),
  })),
}));

describe('createClient — extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should create client with devtools enabled', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      devtools: { enabled: true },
    });

    expect(client).toBeDefined();
    expect(typeof client.request).toBe('function');
    expect(typeof client.getBaseUrl).toBe('function');
  });

  it('should create client with auth config', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => 'token',
        getRefreshToken: async () => 'refresh',
      },
    });

    expect(client).toBeDefined();
  });

  it('should make successful request and unwrap ApiResponse', async () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
    });

    const result = await client.request({ path: '/test', method: 'GET' });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data).toEqual({ message: 'success', path: '/test' });
    }
  });

  it('should work with tenant config', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      tenant: {
        getTenantId: async () => 'tid',
      },
    });

    expect(client).toBeDefined();
  });

  it('should use custom timeout', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      timeout: 10000,
    });

    expect(client).toBeDefined();
  });

  it('should accept custom interceptors', () => {
    const interceptor = {
      onRequest: vi.fn(async (config: RequestConfig) => config),
    };

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      interceptors: [interceptor],
    });

    expect(client).toBeDefined();
  });

  it('should return baseUrl from getBaseUrl', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
    });

    expect(client.getBaseUrl()).toBe('http://localhost:5000');
  });

  it('should return TOKEN_MISSING error when auth required but no token', async () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => null,
        onAuthenticationRequired: vi.fn(),
      },
    });

    const result = await client.request({
      path: '/protected',
      method: 'GET',
      requiresAuth: true,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('TOKEN_MISSING');
      expect(result.error.status).toBe(401);
    }
  });

  it('should deduplicate GET requests', async () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      deduplication: { enabled: true },
    });

    // Make two simultaneous GET requests to the same path
    const [result1, result2] = await Promise.all([
      client.request({ path: '/users', method: 'GET' }),
      client.request({ path: '/users', method: 'GET' }),
    ]);

    expect(result1.ok).toBe(true);
    expect(result2.ok).toBe(true);
  });

  it('should not deduplicate POST requests', async () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
    });

    const result = await client.request({
      path: '/users',
      method: 'POST',
      body: { name: 'test' },
    });

    expect(result.ok).toBe(true);
  });

  it('should handle error results from transport', async () => {
    // Re-import with failed transport
    const { createFetchTransport } = await import('../../src/runtime/transport/fetch.js');
    vi.mocked(createFetchTransport).mockReturnValueOnce({
      request: vi.fn(async () => ({
        ok: false as const,
        error: {
          name: 'ApiError',
          message: 'Server error',
          status: 500,
          code: 'SERVER_ERROR',
        } satisfies ApiError,
      })),
    });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
    });

    const result = await client.request({ path: '/fail', method: 'GET' });
    // Transport returns error result which is passed through
    expect(result).toBeDefined();
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toEqual({
        name: 'ApiError',
        message: 'Server error',
        status: 500,
        code: 'SERVER_ERROR',
      });
    }
  });

  it('should create client with autoRefresh disabled', () => {
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => 'token',
        getRefreshToken: async () => 'refresh',
      },
      autoRefresh: false,
    });

    expect(client).toBeDefined();
  });
});
