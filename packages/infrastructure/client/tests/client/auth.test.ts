/**
 * Client tests - Auth validation
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createClient } from '../../src/client.js';

type AuthFetchResponse = Pick<Response, 'ok' | 'status'> & {
  headers?: Headers;
  json: () => Promise<{ success: boolean }>;
};
type AuthFetchFixture = (...args: Parameters<typeof fetch>) => Promise<AuthFetchResponse>;
const mockFetch = vi.fn<AuthFetchFixture>();

describe('Client - Auth Validation', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    vi.stubGlobal('fetch', mockFetch);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('should return error when auth required but no token', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ success: true }),
    });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => null, // No token
      },
    });

    const result = await client.request({
      method: 'GET',
      path: '/protected',
      requiresAuth: true,
    });

    expect(result.ok).toBe(false);
    expect(mockFetch).not.toHaveBeenCalled();
    if (!result.ok) {
      expect(result.error.code).toBe('TOKEN_MISSING');
      expect(result.error.message).toContain('Authentication required');
    }
  });

  it('should allow authenticated requests with token provider', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ success: true }),
    });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
      auth: {
        getAccessToken: async () => 'valid-token',
      },
    });

    const result = await client.request({
      method: 'GET',
      path: '/protected',
    });

    expect(result.ok).toBe(true);
    expect(global.fetch).toHaveBeenCalled();
    
    // Check Authorization header was added
    const headers = mockFetch.mock.calls[0]?.[1]?.headers;
    expect(headers).toBeInstanceOf(Headers);
    if (!(headers instanceof Headers)) throw new Error('Expected native request Headers');
    expect(headers.get('Authorization')).toBe('Bearer valid-token');
  });

  it('should allow non-auth requests without token provider', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ success: true }),
    });

    const client = createClient({
      baseUrl: 'http://localhost:5000',
    });

    const result = await client.request({
      method: 'GET',
      path: '/public',
    });

    expect(result.ok).toBe(true);
    expect(global.fetch).toHaveBeenCalled();
  });
});
