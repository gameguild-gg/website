import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';

describe('configured API origin in production', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('ALLOW_UNSAFE_REMOTE_URL', undefined);
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockReset();
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ ready: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it.each(['http://backend:8080', 'http://api:8080', 'http://127.0.0.1:8080'])('retains the explicitly configured API %s', async (baseUrl) => {
    const transport = createFetchTransport({ baseUrl });

    const result = await transport.request({ method: 'GET', path: '/v1/ready' });

    expect(result.ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0][0].toString()).toBe(`${baseUrl}/v1/ready`);
  });
});
