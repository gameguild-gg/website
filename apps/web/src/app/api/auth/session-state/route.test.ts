import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ getToken: vi.fn(), fetch: vi.fn() }));
vi.mock('@/auth', () => ({ getToken: mocks.getToken }));

import { GET } from './route';

describe('session-state proxy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', mocks.fetch);
    vi.stubEnv('API_URL', 'http://localhost:8080');
    mocks.getToken.mockResolvedValue('session-token');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('forwards the caller token to the upstream session-state endpoint', async () => {
    mocks.fetch.mockResolvedValue(
      new Response(JSON.stringify({ userId: 'u', sessionValid: true }), { status: 200 }),
    );
    const request = new NextRequest('http://localhost/api/auth/session-state');
    const response = await GET(request);

    expect(response.status).toBe(200);
    expect(mocks.fetch).toHaveBeenCalledWith(
      'http://localhost:8080/v1/auth/session-state',
      expect.objectContaining({
        cache: 'no-store',
        headers: { authorization: 'Bearer session-token' },
        signal: request.signal,
      }),
    );
    expect(response.headers.get('cache-control')).toBe('no-store');
    await expect(response.json()).resolves.toEqual({ userId: 'u', sessionValid: true });
  });

  it('rejects unauthenticated callers before any upstream call', async () => {
    mocks.getToken.mockResolvedValue(null);
    const response = await GET(new NextRequest('http://localhost/api/auth/session-state'));

    expect(response.status).toBe(401);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });

  it('passes upstream failures through with their status', async () => {
    mocks.fetch.mockResolvedValue(new Response(JSON.stringify({ detail: 'stale token' }), { status: 401 }));
    const response = await GET(new NextRequest('http://localhost/api/auth/session-state'));

    expect(response.status).toBe(401);
  });
});
