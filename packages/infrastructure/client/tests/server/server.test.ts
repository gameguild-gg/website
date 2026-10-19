/**
 * Server Client Tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createServerClient } from '../../src/server.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

// Mock the transport module
vi.mock('../../src/runtime/transport/fetch.js', () => ({
  createFetchTransport: vi.fn((config: any) => ({
    request: vi.fn(async (reqConfig: any) => {
      // Simulate a successful response by default
      return { ok: true, data: { data: { message: 'success' } }, status: 200, headers: new Headers() };
    }),
  })),
  createHeaderInterceptor: vi.fn((fn: any) => fn),
}));

describe('createServerClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Re-import to get fresh mocks
    vi.clearAllMocks();
  });

  it('should create a client with baseUrl', () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
    });

    expect(client).toBeDefined();
    expect(client.getBaseUrl()).toBe('http://localhost:5295');
    expect(typeof client.request).toBe('function');
  });

  it('should accept optional interceptors', () => {
    const interceptor = vi.fn((config: RequestConfig) => config);

    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      interceptors: [{ onRequest: interceptor }],
    });

    expect(client).toBeDefined();
  });

  it('should create client with auth provider', () => {
    const auth = {
      getAccessToken: vi.fn().mockResolvedValue('access-token'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      auth,
    });

    expect(client).toBeDefined();
  });

  it('should create client with tenant provider', () => {
    const tenant = {
      getTenantId: vi.fn().mockResolvedValue('tenant-1'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      tenant,
    });

    expect(client).toBeDefined();
  });

  it('should make successful requests', async () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
    });

    const result = await client.request({
      method: 'GET',
      path: '/test',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data).toEqual({ message: 'success' });
    }
  });

  it('should return auth error when token is missing and requiresAuth is true', async () => {
    const auth = {
      getAccessToken: vi.fn().mockResolvedValue(null),
      onAuthenticationRequired: vi.fn(),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      auth,
    });

    const result = await client.request({
      method: 'GET',
      path: '/secure',
      requiresAuth: true,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('TOKEN_MISSING');
      expect(result.error.status).toBe(401);
    }
    expect(auth.onAuthenticationRequired).toHaveBeenCalled();
  });

  it('should proceed with request when token is available for requiresAuth', async () => {
    const auth = {
      getAccessToken: vi.fn().mockResolvedValue('valid-token'),
    };

    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      auth,
    });

    const result = await client.request({
      method: 'GET',
      path: '/secure',
      requiresAuth: true,
    });

    expect(result.ok).toBe(true);
  });

  it('should skip auth check when no auth provider configured', async () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
    });

    // Should not throw even with requiresAuth=true if no auth provider
    const result = await client.request({
      method: 'GET',
      path: '/secure',
      requiresAuth: true,
    });

    expect(result.ok).toBe(true);
  });

  it('should create client without optional fields', () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
    });

    expect(client.getBaseUrl()).toBe('http://localhost:5295');
  });

  it('should accept timeout configuration', () => {
    const client = createServerClient({
      baseUrl: 'http://localhost:5295',
      timeout: 5000,
    });

    expect(client).toBeDefined();
  });
});
