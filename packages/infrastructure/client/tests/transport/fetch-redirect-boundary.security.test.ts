import { createServer, type IncomingMessage, type Server } from 'node:http';
import { once } from 'node:events';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';

type Receipt = { method: string | undefined; url: string | undefined; tenant: string | undefined; contentType: string | undefined; body: string };

async function receipt(request: IncomingMessage): Promise<Receipt> {
  const chunks: Buffer[] = [];
  for await (const chunk of request) chunks.push(Buffer.from(chunk));
  return {
    method: request.method,
    url: request.url,
    tenant: request.headers['x-tenant-id'] as string | undefined,
    contentType: request.headers['content-type'],
    body: Buffer.concat(chunks).toString('utf8'),
  };
}

async function listen(server: Server): Promise<string> {
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  const address = server.address();
  if (!address || typeof address === 'string') throw new Error('Expected an owned TCP test server');
  return `http://127.0.0.1:${address.port}`;
}

async function close(server: Server): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    server.close((error) => (error ? reject(error) : resolve()));
    server.closeAllConnections();
  });
}

describe('HTTP redirect boundary for the configured API origin', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'production');
    vi.stubEnv('ALLOW_UNSAFE_REMOTE_URL', undefined);
  });

  afterEach(() => vi.unstubAllEnvs());

  it.each([301, 302, 303, 307, 308].flatMap((status) => (['GET', 'POST'] as const).map((method) => ({ status, method }))))(
    'does not follow $status redirects for $method to another origin',
    async ({ status, method }) => {
      const redirectedRequests: Receipt[] = [];
      const destination = createServer(async (request, response) => {
        redirectedRequests.push(await receipt(request));
        response.writeHead(200, { 'Content-Type': 'application/json' });
        response.end(JSON.stringify({ accepted: true }));
      });
      const destinationUrl = await listen(destination);
      const originRequests: Receipt[] = [];
      const origin = createServer(async (request, response) => {
        originRequests.push(await receipt(request));
        response.writeHead(status, { Location: `${destinationUrl}/private-target` });
        response.end();
      });
      try {
        const baseUrl = await listen(origin);
        const transport = createFetchTransport({ baseUrl, timeout: 5000, headers: { 'X-Tenant-Id': 'test-tenant' } });
        const result = await transport.request({ method, path: '/v1/redirect', ...(method === 'POST' ? { body: { privateValue: 'test-only-value' } } : {}) });

        expect.soft(result.ok).toBe(false);
        expect.soft(redirectedRequests).toEqual([]);
        expect(originRequests).toHaveLength(1);
        expect(originRequests[0].tenant).toBe('test-tenant');
        expect(originRequests[0].body).toBe(method === 'POST' ? JSON.stringify({ privateValue: 'test-only-value' }) : '');
      } finally {
        await close(origin);
        await close(destination);
      }
    },
  );

  it.each(['GET', 'POST', 'multipart'] as const)('preserves normal %s requests to the configured origin', async (kind) => {
    const requests: Receipt[] = [];
    const origin = createServer(async (request, response) => {
      requests.push(await receipt(request));
      response.writeHead(200, { 'Content-Type': 'application/json' });
      response.end(JSON.stringify({ accepted: true }));
    });
    try {
      const baseUrl = await listen(origin);
      const transport = createFetchTransport({ baseUrl, timeout: 5000, headers: { 'X-Tenant-Id': 'test-tenant' } });
      const multipart = new FormData();
      multipart.set('file', new Blob(['test-file-content'], { type: 'text/plain' }), 'test.txt');
      const result = await transport.request<{ accepted: boolean }>({
        method: kind === 'GET' ? 'GET' : 'POST',
        path: '/v1/content',
        params: { label: 'test value' },
        signal: AbortSignal.timeout(5000),
        ...(kind === 'POST' ? { body: { value: 'test-only-value' } } : kind === 'multipart' ? { body: multipart } : {}),
      });
      expect(result.ok).toBe(true);
      if (result.ok) expect(result.data.data).toEqual({ accepted: true });
      expect(requests).toHaveLength(1);
      expect(requests[0].tenant).toBe('test-tenant');
      expect(requests[0].url).toBe('/v1/content?label=test+value');
      if (kind === 'POST') expect(requests[0].body).toBe(JSON.stringify({ value: 'test-only-value' }));
      if (kind === 'multipart') {
        expect(requests[0].contentType).toMatch(/^multipart\/form-data; boundary=/);
        expect(requests[0].body).toContain('test-file-content');
      }
    } finally {
      await close(origin);
    }
  });
});
