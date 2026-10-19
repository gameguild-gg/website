import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createServerClient } from '../../src/server.js';
import { createNextClient } from '../../src/integrations/next/index.js';
import type { ApiClient } from '../../src/runtime/client.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

describe('tenant headers through the actual fetch transport', () => {
  let fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock = vi.fn<typeof fetch>().mockImplementation(async () =>
      new Response(JSON.stringify({ accepted: true }), { headers: { 'Content-Type': 'application/json' } }),
    );
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  async function wireHeaders(client: ApiClient, config: RequestConfig = { method: 'GET', path: '/fixture' }) {
    const result = await client.request(config);
    expect(result.ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const call = fetchMock.mock.calls[0];
    if (!call) throw new Error('Expected a real transport fetch call');
    expect(call[0]).toBe('https://api.example.test/fixture');
    return new Headers(call[1]?.headers);
  }

  it('keeps the default server tenant header and unrelated request headers', async () => {
    const client = createServerClient({
      baseUrl: 'https://api.example.test',
      tenant: { getTenantId: async () => 'tenant-fixture' },
    });
    const headers = await wireHeaders(client, {
      method: 'GET', path: '/fixture', headers: { 'X-Request-Fixture': 'preserved' },
    });
    expect(headers.get('X-Tenant-Id')).toBe('tenant-fixture');
    expect(headers.get('X-Custom-Tenant')).toBeNull();
    expect(headers.get('X-Request-Fixture')).toBe('preserved');
  });

  it('uses the configured server tenant header without also sending the default', async () => {
    const client = createServerClient({
      baseUrl: 'https://api.example.test',
      tenant: { getTenantId: async () => 'tenant-fixture' },
      tenantHeader: 'X-Custom-Tenant',
    });
    const headers = await wireHeaders(client);
    expect(headers.get('X-Custom-Tenant')).toBe('tenant-fixture');
    expect(headers.get('X-Tenant-Id')).toBeNull();
  });

  it('preserves bearer authentication and token reuse with a custom tenant header', async () => {
    const getAccessToken = vi.fn(async () => 'fixture-access-token');
    const client = createServerClient({
      baseUrl: 'https://api.example.test',
      tenant: { getTenantId: async () => 'tenant-fixture' },
      tenantHeader: 'X-Custom-Tenant',
      auth: { getAccessToken },
    });
    const headers = await wireHeaders(client, { method: 'GET', path: '/fixture', requiresAuth: true });
    expect(headers.get('Authorization')).toBe('Bearer fixture-access-token');
    expect(headers.get('X-Custom-Tenant')).toBe('tenant-fixture');
    expect(getAccessToken).toHaveBeenCalledTimes(1);
  });

  it('omits both tenant headers when the provider has no tenant', async () => {
    const client = createServerClient({
      baseUrl: 'https://api.example.test',
      tenant: { getTenantId: async () => null },
      tenantHeader: 'X-Custom-Tenant',
    });
    const headers = await wireHeaders(client);
    expect(headers.has('X-Custom-Tenant')).toBe(false);
    expect(headers.has('X-Tenant-Id')).toBe(false);
  });

  it('forwards the existing Next client custom header option to the server transport', async () => {
    const client = createNextClient({
      baseUrl: 'https://api.example.test',
      getTenantId: async () => 'next-tenant-fixture',
      tenantHeader: 'X-Next-Tenant',
    });
    const headers = await wireHeaders(client);
    expect(headers.get('X-Next-Tenant')).toBe('next-tenant-fixture');
    expect(headers.get('X-Tenant-Id')).toBeNull();
  });

  it('keeps the default Next client tenant header when no custom name is supplied', async () => {
    const client = createNextClient({
      baseUrl: 'https://api.example.test',
      getTenantId: async () => 'next-tenant-fixture',
    });
    const headers = await wireHeaders(client);
    expect(headers.get('X-Tenant-Id')).toBe('next-tenant-fixture');
  });

  it('retains configured request interceptors alongside tenant propagation', async () => {
    const onRequest = vi.fn((config: RequestConfig): RequestConfig => ({
      ...config, headers: { ...config.headers, 'X-Interceptor-Fixture': 'preserved' },
    }));
    const client = createServerClient({
      baseUrl: 'https://api.example.test',
      tenant: { getTenantId: async () => 'tenant-fixture' },
      tenantHeader: 'X-Custom-Tenant',
      interceptors: [{ onRequest }],
    });
    const headers = await wireHeaders(client);
    expect(onRequest).toHaveBeenCalledTimes(1);
    expect(headers.get('X-Custom-Tenant')).toBe('tenant-fixture');
    expect(headers.get('X-Interceptor-Fixture')).toBe('preserved');
  });
});
