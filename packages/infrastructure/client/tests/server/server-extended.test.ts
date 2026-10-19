/**
 * Extended Server Client Tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createServerClient } from '../../src/server.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { Result } from '../../src/runtime/result/types.js';
import type { ApiResponse, Interceptor, RequestConfig, TransportConfig } from '../../src/runtime/transport/types.js';

type LegacyFixtureError = Omit<ApiError, 'code'> & { code: ApiError['code'] | 'INTERNAL' };
type FixtureTransportResult = Result<ApiResponse<Record<string, string>>, LegacyFixtureError> & {
  status?: number;
  headers?: Headers;
};
type HeaderProvider = Parameters<typeof import('../../src/runtime/transport/fetch.js').createHeaderInterceptor>[0];

const { createFetchTransportMock } = vi.hoisted(() => ({
  createFetchTransportMock: vi.fn((config: TransportConfig) => {
    const interceptors = config.interceptors || [];
    return {
      request: vi.fn(async (reqConfig: RequestConfig): Promise<FixtureTransportResult> => {
        // Run interceptors
        let finalConfig = reqConfig;
        for (const interceptor of interceptors) {
          if (interceptor.onRequest) finalConfig = await interceptor.onRequest(finalConfig);
        }
        return {
          ok: true,
          data: {
            data: finalConfig.headers || {},
            status: 200,
            headers: new Headers(),
          },
        };
      }),
    };
  }),
}));

// Match the native interceptor and response shapes without executing fetch.
vi.mock('../../src/runtime/transport/fetch.js', () => ({
  createFetchTransport: createFetchTransportMock,
  createHeaderInterceptor: vi.fn((fn: HeaderProvider): Interceptor => ({
    onRequest: async (config) => {
      const headers = await fn();
      return {
        ...config,
        headers: { ...config.headers, ...headers },
      };
    },
  })),
}));

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
    expect(createFetchTransport).toBe(createFetchTransportMock);
    const mockCalls = createFetchTransportMock.mock.results;
    const latestResult = mockCalls[mockCalls.length - 1];
    if (!latestResult || latestResult.type !== 'return') throw new Error('Expected a created transport fixture');
    const latestTransport = latestResult.value;

    // Override request to return error for the next call
    // Preserve the existing backend's nonstandard INTERNAL code in this fixture.
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

  it('should return native response metadata from requestRaw', async () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant: { getTenantId: vi.fn(async () => 'raw-tenant') },
    });

    const result = await client.requestRaw<Record<string, string>>({ path: '/raw', method: 'GET' });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.status).toBe(200);
      expect(result.data.headers).toBeInstanceOf(Headers);
      expect(result.data.data).toEqual({ 'X-Tenant-Id': 'raw-tenant' });
    }
  });

  it('should retain each transport fixture interceptor when another client is created', async () => {
    const first = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant: { getTenantId: vi.fn(async () => 'first-tenant') },
    });
    const second = createServerClient({
      baseUrl: 'http://localhost:8080',
      tenant: { getTenantId: vi.fn(async () => 'second-tenant') },
    });

    const firstResult = await first.request<Record<string, string>>({ path: '/first', method: 'GET' });
    const secondResult = await second.request<Record<string, string>>({ path: '/second', method: 'GET' });

    expect(firstResult.ok).toBe(true);
    expect(secondResult.ok).toBe(true);
    if (firstResult.ok) expect(firstResult.data).toEqual({ 'X-Tenant-Id': 'first-tenant' });
    if (secondResult.ok) expect(secondResult.data).toEqual({ 'X-Tenant-Id': 'second-tenant' });
  });
});
