import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ getRequestAuthContext: vi.fn(), fetch: vi.fn() }));
vi.mock('@/auth', () => ({ getRequestAuthContext: mocks.getRequestAuthContext }));

import { GET } from './route';

describe('audit replay capture proxy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', mocks.fetch);
    vi.stubEnv('API_URL', 'http://localhost:8080');
    mocks.getRequestAuthContext.mockResolvedValue({
      session: { user: { id: 'admin' } },
      token: 'admin-token',
      tenantId: '50000000-0000-0000-0000-000000000490',
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  function upstreamSnapshot(): Response {
    return new Response('{"schemaVersion":1}', {
      status: 200,
      headers: { 'X-Audit-Snapshot-SHA256': 'a'.repeat(64), 'content-type': 'application/json' },
    });
  }

  it('forwards the range and caller token to the guarded GET capture endpoint', async () => {
    mocks.fetch.mockResolvedValue(upstreamSnapshot());
    const request = new NextRequest('http://localhost/api/audit/replay?start=2026-10-13T09%3A00%3A00Z&end=2026-10-13T10%3A00%3A00Z');
    const response = await GET(request);

    expect(response.status).toBe(200);
    expect(mocks.fetch).toHaveBeenCalledWith(
      'http://localhost:8080/v1/admin/audit-logs/replay/snapshots?start=2026-10-13T09%3A00%3A00Z&end=2026-10-13T10%3A00%3A00Z',
      expect.objectContaining({
        cache: 'no-store',
        headers: { authorization: 'Bearer admin-token', accept: 'application/json' },
        signal: request.signal,
      }),
    );
    const body = (await response.json()) as { json: string; sha256: string; tenantId: string | null };
    expect(body.json).toBe('{"schemaVersion":1}');
    expect(body.sha256).toBe('a'.repeat(64));
    expect(body.tenantId).toBe('50000000-0000-0000-0000-000000000490');
  });

  it('rejects unauthenticated callers before any upstream call', async () => {
    mocks.getRequestAuthContext.mockResolvedValue({ session: null, token: null, tenantId: null });
    const response = await GET(new NextRequest('http://localhost/api/audit/replay?start=a&end=b'));

    expect(response.status).toBe(401);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });

  it('requires both range endpoints', async () => {
    const missingEnd = await GET(new NextRequest('http://localhost/api/audit/replay?start=2026-10-13T09%3A00%3A00Z'));
    expect(missingEnd.status).toBe(400);
    const missingBoth = await GET(new NextRequest('http://localhost/api/audit/replay'));
    expect(missingBoth.status).toBe(400);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });

  it('fails closed when the capture returns no digest', async () => {
    mocks.fetch.mockResolvedValue(new Response('{}', { status: 200 }));
    const response = await GET(new NextRequest('http://localhost/api/audit/replay?start=a&end=b'));

    expect(response.status).toBe(502);
  });

  it('passes upstream rejections through with their status', async () => {
    mocks.fetch.mockResolvedValue(new Response(JSON.stringify({ title: 'Range' }), { status: 400 }));
    const response = await GET(new NextRequest('http://localhost/api/audit/replay?start=a&end=b'));

    expect(response.status).toBe(400);
  });
});
