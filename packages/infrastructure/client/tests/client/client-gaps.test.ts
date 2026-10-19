/**
 * Client gap tests — covers client.ts lines 75, 82, 91-95
 * Uses REAL transport with mocked global.fetch so interceptor closures execute.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createClient } from '../../src/client.js';

describe('createClient — interceptor gap coverage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function mockFetch(responseData: unknown = { ok: true }, status = 200) {
    vi.stubGlobal('fetch', vi.fn<typeof globalThis.fetch>(async () => new Response(JSON.stringify(responseData), {
      status,
      headers: { 'content-type': 'application/json' },
    })));
  }

  function capturedOptions() {
    const call = vi.mocked(globalThis.fetch).mock.calls[0];
    if (!call) throw new Error('Expected the client to call fetch');
    const options = call[1];
    if (!(options?.headers instanceof Headers)) {
      throw new Error('Expected native Headers in the fetch request');
    }
    return { ...options, headers: options.headers };
  }

  it('creates refreshManager and calls refreshIfNeeded on request (lines 75, 82)', async () => {
    mockFetch({ data: 'ok' });

    let refreshCalled = false;
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => 'access-tok',
        getRefreshToken: async () => 'refresh-tok',
        onTokenRefresh: async () => {
          refreshCalled = true;
        },
      },
    });

    // Making a request triggers the auth interceptor closure
    // which calls refreshManager.refreshIfNeeded() (line 82)
    const result = await client.request({ path: '/api/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.data).toEqual({ data: 'ok' });

    // fetch should have been called with Authorization header
    expect(globalThis.fetch).toHaveBeenCalled();
    const opts = capturedOptions();
    expect(opts.headers.get('Authorization')).toBe('Bearer access-tok');
  });

  it('auth interceptor returns empty headers when getAccessToken returns null (line 88)', async () => {
    mockFetch({ data: 'ok' });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => null,
      },
    });

    await client.request({ path: '/api/test', method: 'GET', headers: {} });

    const opts = capturedOptions();
    // No Authorization header should be set
    expect(opts.headers.has('Authorization')).toBe(false);
  });

  it('tenant interceptor sets X-Tenant-Id header (lines 91-94)', async () => {
    mockFetch({ data: 'ok' });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      tenant: {
        getTenantId: async () => 'tenant-abc',
      },
    });

    await client.request({ path: '/api/test', method: 'GET', headers: {} });

    const opts = capturedOptions();
    expect(opts.headers.get('X-Tenant-Id')).toBe('tenant-abc');
  });

  it('tenant interceptor returns empty headers when tenantId is null (line 95)', async () => {
    mockFetch({ data: 'ok' });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      tenant: {
        getTenantId: async () => null,
      },
    });

    await client.request({ path: '/api/test', method: 'GET', headers: {} });

    const opts = capturedOptions();
    expect(opts.headers.has('X-Tenant-Id')).toBe(false);
  });

  it('returns TOKEN_MISSING error when requiresAuth but no token', async () => {
    const onAuthRequired = vi.fn();
    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => null,
        onAuthenticationRequired: onAuthRequired,
      },
    });

    const result = await client.request({
      path: '/protected',
      method: 'GET',
      requiresAuth: true,
      headers: {},
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('TOKEN_MISSING');
    }
    expect(onAuthRequired).toHaveBeenCalled();
  });

  it('auth + tenant interceptors together on a request', async () => {
    mockFetch({ data: 'ok' });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => 'tok-123',
        getRefreshToken: async () => 'ref-456',
      },
      tenant: {
        getTenantId: async () => 'tn-789',
      },
    });

    await client.request({ path: '/api/resource', method: 'GET', headers: {} });

    const opts = capturedOptions();
    expect(opts.headers.get('Authorization')).toBe('Bearer tok-123');
    expect(opts.headers.get('X-Tenant-Id')).toBe('tn-789');
  });
});
