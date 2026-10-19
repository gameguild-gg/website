/**
 * Extended Fetch Transport Tests — timeout, text responses, buildUrl, parse errors
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createFetchTransport, createHeaderInterceptor } from '../../src/runtime/transport/fetch.js';

describe('createFetchTransport — extended', () => {
  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('should handle text response when content-type is not JSON', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('plain text response', {
        status: 200,
        headers: { 'Content-Type': 'text/plain' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/text',
      method: 'GET',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      // Transport wraps in ApiResponse: { data, status, headers }
      expect(result.data.data).toBe('plain text response');
      expect(result.data.status).toBe(200);
    }
  });

  it('should preserve downloadable attachments as Blob values', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('event,applications\nAugust lab,2', {
        status: 200,
        headers: {
          'Content-Disposition': 'attachment; filename="testing-lab.csv"',
          'Content-Type': 'text/csv; charset=utf-8',
        },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request<Blob>({
      path: '/analytics/export',
      method: 'GET',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.data).toBeInstanceOf(Blob);
      await expect(result.data.data.text()).resolves.toBe('event,applications\nAugust lab,2');
    }
  });

  it('should handle 204 No Content response', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response(null, { status: 204 });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/delete',
      method: 'DELETE',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.status).toBe(204);
      expect(result.data.data).toBeUndefined();
    }
  });

  it('should handle JSON parse error', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('not valid json{', {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/bad-json',
      method: 'GET',
    });

    // Should return an error result for parse failure
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('PARSE_ERROR');
    }
  });

  it('should handle timeout with AbortController', async () => {
    globalThis.fetch = vi.fn(async (_url: Parameters<typeof fetch>[0], opts?: RequestInit) => {
      return new Promise<Response>((_, reject) => {
        const abortSignal = opts?.signal;
        if (abortSignal) {
          abortSignal.addEventListener('abort', () => {
            reject(new DOMException('The operation was aborted', 'AbortError'));
          });
        }
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
      timeout: 50,
    });

    const result = await transport.request({
      path: '/slow',
      method: 'GET',
    });

    expect(result.ok).toBe(false);
  });

  it('should build URL with query parameters', async () => {
    let capturedUrl = '';
    globalThis.fetch = vi.fn(async (url: any) => {
      capturedUrl = url.toString();
      return new Response(JSON.stringify({ data: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    await transport.request({
      path: '/api/users',
      method: 'GET',
      params: { page: '1', pageSize: '10', search: 'test user' },
    });

    expect(capturedUrl).toContain('page=1');
    expect(capturedUrl).toContain('pageSize=10');
    expect(capturedUrl).toContain('search=test');
  });

  it('should serialize array query parameters as repeated keys', async () => {
    let capturedUrl = '';
    globalThis.fetch = vi.fn(async (url: any) => {
      capturedUrl = url.toString();
      return new Response(JSON.stringify({ data: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    await transport.request({
      path: '/api/eligibility',
      method: 'GET',
      params: { testerUserIds: ['tester-1', 'tester-2'] },
    });

    expect(capturedUrl).toContain('testerUserIds=tester-1&testerUserIds=tester-2');
  });

  it('should omit undefined array query values without dropping defined values', async () => {
    let capturedUrl = '';
    globalThis.fetch = vi.fn(async (url: any) => {
      capturedUrl = url.toString();
      return new Response(JSON.stringify({ data: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });
    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    await transport.request({
      path: '/api/tags',
      method: 'GET',
      params: { tag: ['one', undefined, 'two'] },
    });

    expect(capturedUrl).toContain('tag=one&tag=two');
    expect(capturedUrl).not.toContain('undefined');
  });

  it('should send request body as JSON for POST', async () => {
    let capturedBody = '';
    globalThis.fetch = vi.fn(async (_url: any, opts: any) => {
      capturedBody = opts.body;
      return new Response(JSON.stringify({ id: '1' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    await transport.request({
      path: '/api/users',
      method: 'POST',
      body: { name: 'Test', email: 'test@example.com' },
    });

    const parsed = JSON.parse(capturedBody);
    expect(parsed.name).toBe('Test');
    expect(parsed.email).toBe('test@example.com');
  });

  it('should handle non-ok HTTP responses', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response(JSON.stringify({ message: 'Not Found' }), {
        status: 404,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/api/nonexistent',
      method: 'GET',
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.status).toBe(404);
    }
  });

  it('should handle network errors', async () => {
    globalThis.fetch = vi.fn(async () => {
      throw new Error('Network failure');
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/api/data',
      method: 'GET',
    });

    expect(result.ok).toBe(false);
  });

  it('should merge custom headers', async () => {
    let capturedHeaders: any = {};
    globalThis.fetch = vi.fn(async (_url: any, opts: any) => {
      capturedHeaders = Object.fromEntries(new Headers(opts.headers).entries());
      return new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    await transport.request({
      path: '/api/data',
      method: 'GET',
      headers: { 'X-Custom': 'value', Authorization: 'Bearer token' },
    });

    expect(capturedHeaders['x-custom']).toBe('value');
    expect(capturedHeaders['authorization']).toBe('Bearer token');
  });

  it('should handle empty response body with JSON content-type', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('', {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      path: '/api/empty',
      method: 'GET',
    });

    // Should handle gracefully (either parse error or empty result)
    expect(result).toBeDefined();
  });
});

describe('createHeaderInterceptor', () => {
  it('should create an interceptor object with onRequest', () => {
    const interceptor = createHeaderInterceptor(async () => ({
      Authorization: 'Bearer test',
      'X-Custom': 'value',
    }));

    // createHeaderInterceptor returns an Interceptor object, not a function
    expect(typeof interceptor).toBe('object');
    expect(interceptor.onRequest).toBeDefined();
    expect(typeof interceptor.onRequest).toBe('function');
  });

  it('should add headers to request config via onRequest', async () => {
    const interceptor = createHeaderInterceptor(async () => ({
      Authorization: 'Bearer my-token',
    }));

    const config = { path: '/test', method: 'GET' as const, headers: {} };
    const result = await interceptor.onRequest!(config);

    expect(result.headers).toBeDefined();
    expect((result.headers as any)['Authorization']).toBe('Bearer my-token');
  });
});

describe('createFetchTransport — metrics key propagation', () => {
  let originalFetch: typeof globalThis.fetch;

  beforeEach(() => {
    originalFetch = globalThis.fetch;
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('should propagate _metricsKey on error responses', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response(JSON.stringify({ message: 'Bad Request' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    const result = await transport.request({
      path: '/api/data',
      method: 'POST',
      body: { invalid: true },
      _metricsKey: 'POST:/api/data:123',
    } as any);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect((result.error as any)._metricsKey).toBe('POST:/api/data:123');
    }
  });

  it('should propagate _metricsKey on successful responses', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response(JSON.stringify({ id: 1 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    const result = await transport.request({
      path: '/api/data',
      method: 'GET',
      _metricsKey: 'GET:/api/data:456',
    } as any);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect((result.data as any)._metricsKey).toBe('GET:/api/data:456');
    }
  });

  it('should propagate _metricsKey on JSON parse errors', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('not json{{{', {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    const result = await transport.request({
      path: '/api/data',
      method: 'GET',
      _metricsKey: 'GET:/api/data:789',
    } as any);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('PARSE_ERROR');
      expect((result.error as any)._metricsKey).toBe('GET:/api/data:789');
    }
  });

  it('should not include body for GET requests', async () => {
    let capturedOpts: any;
    globalThis.fetch = vi.fn(async (_url: any, opts: any) => {
      capturedOpts = opts;
      return new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    await transport.request({
      path: '/api/data',
      method: 'GET',
      body: { shouldBeIgnored: true },
    });

    expect(capturedOpts.body).toBeUndefined();
  });

  it('should not include body for HEAD requests', async () => {
    let capturedOpts: any;
    globalThis.fetch = vi.fn(async (_url: any, opts: any) => {
      capturedOpts = opts;
      return new Response(null, { status: 200 });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    await transport.request({
      path: '/api/data',
      method: 'HEAD',
      body: { shouldBeIgnored: true },
    });

    expect(capturedOpts.body).toBeUndefined();
  });

  it('should handle Content-Length 0 as no content', async () => {
    globalThis.fetch = vi.fn(async () => {
      return new Response('', {
        status: 200,
        headers: { 'Content-Length': '0', 'Content-Type': 'application/json' },
      });
    });

    const transport = createFetchTransport({ baseUrl: 'http://localhost:8080' });

    const result = await transport.request({
      path: '/api/data',
      method: 'GET',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.data).toBeUndefined();
    }
  });
});
