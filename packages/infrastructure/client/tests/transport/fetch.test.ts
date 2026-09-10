/**
 * Fetch transport tests - Request ID generation
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

// Mock fetch
global.fetch = vi.fn();

describe('Fetch Transport - Request ID', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (global.fetch as any).mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: 'test' }),
    });
  });

  it('should generate request ID automatically', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const request: RequestConfig = {
      method: 'GET',
      path: '/test',
    };

    await transport.request(request);

    expect(global.fetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.any(Headers),
      }),
    );

    const headers = (global.fetch as any).mock.calls[0][1].headers as Headers;
    expect(headers.get('X-Request-Id')).toBeTruthy();
    expect(headers.get('X-Request-Id')).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
  });

  it('should use provided request ID', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const customId = 'custom-request-id-12345';
    const request: RequestConfig = {
      method: 'GET',
      path: '/test',
      requestId: customId,
    };

    await transport.request(request);

    const headers = (global.fetch as any).mock.calls[0][1].headers as Headers;
    expect(headers.get('X-Request-Id')).toBe(customId);
  });

  it('should disable request ID generation when configured', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
      generateRequestId: false,
    });

    const request: RequestConfig = {
      method: 'GET',
      path: '/test',
    };

    await transport.request(request);

    const headers = (global.fetch as any).mock.calls[0][1].headers as Headers;
    expect(headers.get('X-Request-Id')).toBeNull();
  });

  it('should use custom request ID generator', async () => {
    let counter = 0;
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
      requestIdGenerator: () => `custom-${++counter}`,
    });

    await transport.request({ method: 'GET', path: '/test1' });
    await transport.request({ method: 'GET', path: '/test2' });

    const headers1 = (global.fetch as any).mock.calls[0][1].headers as Headers;
    const headers2 = (global.fetch as any).mock.calls[1][1].headers as Headers;

    expect(headers1.get('X-Request-Id')).toBe('custom-1');
    expect(headers2.get('X-Request-Id')).toBe('custom-2');
  });

  it('should handle JSON parse errors gracefully', async () => {
    (global.fetch as any).mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => {
        throw new Error('Invalid JSON');
      },
      text: async () => 'Invalid JSON response body',
    });

    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const result = await transport.request({
      method: 'GET',
      path: '/test',
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('PARSE_ERROR');
      expect(result.error.message).toContain('Failed to parse');
    }
  });

  it('propagates the transport cache policy to fetch', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
      cache: 'no-store',
    });

    await transport.request({ method: 'GET', path: '/dynamic' });

    expect(global.fetch).toHaveBeenCalledWith('http://localhost:8080/dynamic', expect.objectContaining({ cache: 'no-store' }));
  });

  it('sends FormData without JSON encoding or an explicit content type', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });
    const body = new FormData();
    body.set('file', new Blob(['image-bytes'], { type: 'image/png' }), 'story.png');

    await transport.request({ method: 'POST', path: '/upload', body });

    const options = (global.fetch as any).mock.calls[0][1] as RequestInit;
    expect(options.body).toBe(body);
    expect((options.headers as Headers).has('Content-Type')).toBe(false);
  });
});
