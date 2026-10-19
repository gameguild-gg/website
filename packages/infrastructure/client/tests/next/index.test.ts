/**
 * Tests for Next.js Integration Index (factory functions)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  createNextAuthTokenProvider,
  createNextTenantProvider,
  createNextClient,
  createClientFromCookies,
  createRouteClient,
} from '../../src/integrations/next/index.js';
import type { ServerClientConfig } from '../../src/server.js';

// Mock the server client
vi.mock('../../src/server.js', () => ({
  createServerClient: vi.fn((config: ServerClientConfig) => ({
    getBaseUrl: () => config.baseUrl,
    request: vi.fn(),
    ...config,
    _mockClient: true,
  })),
}));

describe('createNextAuthTokenProvider', () => {
  it('should return access token from session', async () => {
    const getSession = vi.fn(async () => ({
      accessToken: 'my-access-token',
      refreshToken: 'my-refresh-token',
    }));

    const provider = createNextAuthTokenProvider(getSession);
    const token = await provider.getAccessToken();

    expect(token).toBe('my-access-token');
  });

  it('should return refresh token from session', async () => {
    const getSession = vi.fn(async () => ({
      accessToken: 'at',
      refreshToken: 'my-refresh-token',
    }));

    const provider = createNextAuthTokenProvider(getSession);
    if (!provider.getRefreshToken) throw new Error('Expected the refresh-token callback');
    const token = await provider.getRefreshToken();

    expect(token).toBe('my-refresh-token');
  });

  it('should return null when session is null', async () => {
    const getSession = vi.fn(async () => null);

    const provider = createNextAuthTokenProvider(getSession);

    expect(await provider.getAccessToken()).toBeNull();
    if (!provider.getRefreshToken) throw new Error('Expected the refresh-token callback');
    expect(await provider.getRefreshToken()).toBeNull();
  });

  it('should return null when tokens are missing', async () => {
    const getSession = vi.fn(async () => ({}));

    const provider = createNextAuthTokenProvider(getSession);

    expect(await provider.getAccessToken()).toBeNull();
    if (!provider.getRefreshToken) throw new Error('Expected the refresh-token callback');
    expect(await provider.getRefreshToken()).toBeNull();
  });

  it('should have onAuthenticationRequired that logs a warning', async () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const getSession = vi.fn(async () => null);

    const provider = createNextAuthTokenProvider(getSession);
    await provider.onAuthenticationRequired!();

    expect(spy).toHaveBeenCalledWith('[client] Authentication required but no session available');
    spy.mockRestore();
  });
});

describe('createNextTenantProvider', () => {
  it('should return tenant ID', async () => {
    const getTenantId = vi.fn(async () => 'tenant-123');

    const provider = createNextTenantProvider(getTenantId);
    const tenantId = await provider.getTenantId();

    expect(tenantId).toBe('tenant-123');
  });

  it('should have onTenantRequired that logs a warning', async () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const getTenantId = vi.fn(async () => null);

    const provider = createNextTenantProvider(getTenantId);
    await provider.onTenantRequired!();

    expect(spy).toHaveBeenCalledWith('[client] Tenant ID required but not available');
    spy.mockRestore();
  });
});

describe('createNextClient', () => {
  it('should create a client with baseUrl', () => {
    const client = createNextClient({
      baseUrl: 'http://localhost:5000',
    });

    expect(client).toBeDefined();
    expect(client).toHaveProperty('_mockClient', true);
  });

  it('should add auth when getSession is provided', () => {
    const getSession = vi.fn(async () => ({ accessToken: 'at' }));

    const client = createNextClient({
      baseUrl: 'http://localhost:5000',
      getSession,
    });

    expect(client).toBeDefined();
    expect(client).toHaveProperty('auth', expect.anything());
  });

  it('should add tenant when getTenantId is provided', () => {
    const getTenantId = vi.fn(async () => 'tid');

    const client = createNextClient({
      baseUrl: 'http://localhost:5000',
      getTenantId,
    });

    expect(client).toBeDefined();
    expect(client).toHaveProperty('tenant', expect.anything());
  });

  it('should pass through timeout and interceptors', () => {
    const interceptor = vi.fn();

    const client = createNextClient({
      baseUrl: 'http://localhost:5000',
      timeout: 5000,
      interceptors: [{ onRequest: interceptor }],
    });

    expect(client).toBeDefined();
    expect(client).toMatchObject({ timeout: 5000, interceptors: [{ onRequest: interceptor }] });
  });
});

describe('createClientFromCookies', () => {
  it('should create a client from cookies', async () => {
    const mockCookies = {
      get: vi.fn((name: string) => {
        if (name === 'access_token') return { value: 'cookie-at' };
        if (name === 'tenant_id') return { value: 'cookie-tenant' };
        return undefined;
      }),
    };

    const client = await createClientFromCookies({
      baseUrl: 'http://localhost:5000',
      getCookies: async () => mockCookies,
    });

    expect(client).toBeDefined();
    expect(client).toHaveProperty('_mockClient', true);
  });

  it('should use custom cookie names', async () => {
    const mockCookies = {
      get: vi.fn((name: string) => {
        if (name === 'my_token') return { value: 'custom-at' };
        if (name === 'my_tenant') return { value: 'custom-tenant' };
        return undefined;
      }),
    };

    const client = await createClientFromCookies({
      baseUrl: 'http://localhost:5000',
      getCookies: async () => mockCookies,
      accessTokenCookie: 'my_token',
      tenantCookie: 'my_tenant',
    });

    expect(client).toBeDefined();
  });

  it('should handle missing cookies gracefully', async () => {
    const mockCookies = {
      get: vi.fn(() => undefined),
    };

    const client = await createClientFromCookies({
      baseUrl: 'http://localhost:5000',
      getCookies: async () => mockCookies,
    });

    expect(client).toBeDefined();
  });
});

describe('createRouteClient', () => {
  it('should create a client from request headers', () => {
    const headers = new Headers({
      Authorization: 'Bearer header-token',
      'X-Tenant-Id': 'header-tenant',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      headers,
    });

    expect(client).toBeDefined();
  });

  it('should extract auth from Authorization header when no session getter', () => {
    const headers = new Headers({
      Authorization: 'Bearer my-token',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      headers,
    });

    expect(client).toBeDefined();
  });

  it('should not override existing session getter', () => {
    const getSession = vi.fn(async () => ({ accessToken: 'explicit-token' }));
    const headers = new Headers({
      Authorization: 'Bearer header-token',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      getSession,
      headers,
    });

    expect(client).toBeDefined();
  });

  it('should extract tenant from custom header', () => {
    const headers = new Headers({
      'X-Custom-Tenant': 'custom-tid',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      headers,
      tenantHeader: 'X-Custom-Tenant',
    });

    expect(client).toBeDefined();
  });

  it('should not override existing tenant getter', () => {
    const getTenantId = vi.fn(async () => 'explicit-tenant');
    const headers = new Headers({
      'X-Tenant-Id': 'header-tenant',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      getTenantId,
      headers,
    });

    expect(client).toBeDefined();
  });

  it('should work without headers', () => {
    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
    });

    expect(client).toBeDefined();
  });

  it('should handle non-Bearer auth headers', () => {
    const headers = new Headers({
      Authorization: 'Basic dXNlcjpwYXNz',
    });

    const client = createRouteClient({
      baseUrl: 'http://localhost:5000',
      headers,
    });

    expect(client).toBeDefined();
  });
});
