/**
 * Extended Server Client Tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createServerClient } from '../../src/server.js';

// We need to avoid the real transport module
vi.mock('../../src/runtime/transport/fetch.js', () => {
  let interceptors: any[] = [];

  return {
    createFetchTransport: vi.fn((config: any) => {
      interceptors = config.interceptors || [];
      return {
        request: vi.fn(async (reqConfig: any) => {
          // Run interceptors
          let finalConfig = reqConfig;
          for (const interceptor of interceptors) {
            finalConfig = await interceptor(finalConfig);
          }
          return {
            ok: true,
            data: { data: finalConfig.headers || {} },
            status: 200,
            headers: new Headers(),
          };
        }),
      };
    }),
    createHeaderInterceptor: vi.fn((fn: any) => {
      return async (config: any) => {
        const headers = await fn();
        return {
          ...config,
          headers: { ...config.headers, ...headers },
        };
      };
    }),
  };
});

describe('createServerClient — extended', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should add auth interceptor when auth provider is given', async () => {
    const auth = {
      getAccessToken: vi.fn(async () => 'test-token'),
      getRefreshToken: vi.fn(async () => null),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      auth,
    });

    expect(client).toBeDefined();
    // The interceptor should be registered
    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result).toBeDefined();
  });

  it('should reuse the token fetched for the auth precheck in the request interceptor', async () => {
    const auth = {
      getAccessToken: vi.fn(async () => 'cached-token'),
      getRefreshToken: vi.fn(async () => null),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      auth,
    });

    const result = await client.request({ path: '/secure', method: 'GET', requiresAuth: true });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data).toEqual({ Authorization: 'Bearer cached-token' });
    }
    expect(auth.getAccessToken).toHaveBeenCalledTimes(1);
  });

  it('should add tenant interceptor when tenant provider is given', async () => {
    const tenant = {
      getTenantId: vi.fn(async () => 'tenant-123'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant,
    });

    expect(client).toBeDefined();
    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result).toBeDefined();
  });

  it('should add both auth and tenant interceptors', async () => {
    const auth = {
      getAccessToken: vi.fn(async () => 'token'),
      getRefreshToken: vi.fn(async () => null),
    };
    const tenant = {
      getTenantId: vi.fn(async () => 'tid'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      auth,
      tenant,
    });

    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result).toBeDefined();
  });

  it('should handle auth provider returning null token', async () => {
    const auth = {
      getAccessToken: vi.fn(async () => null),
      getRefreshToken: vi.fn(async () => null),
      onAuthenticationRequired: vi.fn(),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      auth,
    });

    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result).toBeDefined();
  });

  it('should handle tenant provider returning null', async () => {
    const tenant = {
      getTenantId: vi.fn(async () => null),
      onTenantRequired: vi.fn(),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant,
    });

    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result).toBeDefined();
  });

  it('should pass custom tenant header name', async () => {
    const tenant = {
      getTenantId: vi.fn(async () => 'tid'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant,
      tenantHeader: 'X-Custom-Tenant',
    });

    expect(client).toBeDefined();
    const result = await client.request({ path: '/test', method: 'GET' });
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.data).toEqual({ 'X-Custom-Tenant': 'tid' });
  });

  it('should pass timeout to transport', () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      timeout: 5000,
    });

    expect(client).toBeDefined();
  });

  it('should return error result when transport returns ok:false', async () => {
    const { createFetchTransport } = await import('../../src/runtime/transport/fetch.js');

    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
    });

    // Get the latest mock transport returned by createFetchTransport
    const mockCalls = (createFetchTransport as any).mock.results;
    const latestTransport = mockCalls[mockCalls.length - 1].value;

    // Override request to return error for the next call
    latestTransport.request.mockResolvedValueOnce({
      ok: false,
      error: { name: 'ApiError', status: 500, code: 'INTERNAL', message: 'Server error' },
      status: 500,
      headers: new Headers(),
    });

    const result = await client.request({ path: '/fail', method: 'GET', headers: {} });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('INTERNAL');
    }
  });
});
