/**
 * Fetch transport tests - Request ID generation
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

const mockFetch = vi.fn<typeof fetch>();

function fetchOptions(index = 0): RequestInit {
  const options = mockFetch.mock.calls[index]?.[1];
  if (!options) throw new Error('Expected captured fetch options');
  return options;
}

function fetchHeaders(index = 0): Headers {
  const headers = fetchOptions(index).headers;
  if (!(headers instanceof Headers)) throw new TypeError('Expected native request Headers');
  return headers;
}

describe('Fetch Transport - Request ID', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockReset();
    vi.stubGlobal('fetch', mockFetch);
    mockFetch.mockImplementation(async () => new Response(JSON.stringify({ data: 'test' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('should generate request ID automatically', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
    });

    const request: RequestConfig = {
      method: 'GET',
      path: '/test',
    };

    const result = await transport.request(request);
    expect(result.ok).toBe(true);
    if (result.ok) expect(result.data.data).toEqual({ data: 'test' });

    expect(global.fetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.any(Headers),
      }),
    );

    const headers = fetchHeaders();
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

    const headers = fetchHeaders();
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

    const headers = fetchHeaders();
    expect(headers.get('X-Request-Id')).toBeNull();
  });

  it('should use custom request ID generator', async () => {
    let counter = 0;
    const transport = createFetchTransport({
      baseUrl: 'http://localhost:8080',
      requestIdGenerator: () => `custom-${++counter}`,
    });

    const first = await transport.request({ method: 'GET', path: '/test1' });
    const second = await transport.request({ method: 'GET', path: '/test2' });
    expect(first.ok).toBe(true);
    expect(second.ok).toBe(true);

    const headers1 = fetchHeaders();
    const headers2 = fetchHeaders(1);

    expect(headers1.get('X-Request-Id')).toBe('custom-1');
    expect(headers2.get('X-Request-Id')).toBe('custom-2');
  });

  it('should handle JSON parse errors gracefully', async () => {
    const response = new Response('Invalid JSON response body', {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
    vi.spyOn(response, 'json').mockRejectedValue(new Error('Invalid JSON'));
    mockFetch.mockResolvedValue(response);

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

    const options = fetchOptions();
    expect(options.body).toBe(body);
    expect((options.headers as Headers).has('Content-Type')).toBe(false);
  });
});
