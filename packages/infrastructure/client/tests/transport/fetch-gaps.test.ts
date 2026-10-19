/**
 * Fetch transport gap tests — covers fetch.ts lines 74-75, 86-87, 146
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createFetchTransport } from '../../src/runtime/transport/fetch.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { ApiResponse, Interceptor, RequestConfig } from '../../src/runtime/transport/types.js';

type ResponseFixture = Pick<Response, 'ok' | 'status' | 'headers'> & Partial<Pick<Response, 'statusText' | 'json' | 'text'>>;
type FetchInitFixture = Omit<RequestInit, 'headers'> & { headers: Headers };
type FetchFixture = (input: Parameters<typeof fetch>[0], init: FetchInitFixture) => Promise<ResponseFixture>;
type MetricsRequestConfig = RequestConfig & { _metricsKey: string };
type InterceptedErrorFixture = Omit<ApiError, 'code'> & { code: ApiError['code'] | 'INTERCEPTED' | 'INTERCEPTED_NETWORK' };

function withFixtureCode(error: ApiError, code: 'INTERCEPTED' | 'INTERCEPTED_NETWORK'): ApiError {
  // Keep these existing interceptor markers at the fixture boundary, including
  // codes outside the public ApiError union. Production types stay unchanged.
  const mappedError: InterceptedErrorFixture = { ...error, code };
  return mappedError as ApiError;
}

const mockFetch = vi.fn<FetchFixture>();

beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch);
  mockFetch.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('createFetchTransport — gap coverage', () => {
  it('applies error interceptor on non-ok response (lines 74-75)', async () => {
    const errorInterceptor = vi.fn(async (error: ApiError) => ({
      ok: false as const,
      error: withFixtureCode(error, 'INTERCEPTED'),
    }));

    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      interceptors: [{ onError: errorInterceptor }],
    });

    mockFetch.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      headers: new Headers(),
      json: async () => ({ message: 'Server Error' }),
      text: async () => 'Server Error',
    });

    const result = await transport.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(false);
    expect(errorInterceptor).toHaveBeenCalled();
    if (!result.ok) expect(result.error.code).toBe('INTERCEPTED');
  });

  it('applies response interceptor on success (line 67)', async () => {
    const responseInterceptor = vi.fn<(response: ApiResponse<unknown>) => void>();
    const onResponse: NonNullable<Interceptor['onResponse']> = async (response) => {
      responseInterceptor(response);
      return { ...response, data: { ...response.data, intercepted: true } };
    };

    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      interceptors: [{ onResponse }],
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ hello: 'world' }),
    });

    const result = await transport.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(true);
    expect(responseInterceptor).toHaveBeenCalled();
    if (result.ok) expect(result.data.data).toEqual({ hello: 'world', intercepted: true });
  });

  it('applies error interceptor on network error (lines 86-87)', async () => {
    const errorInterceptor = vi.fn(async (error: ApiError) => ({
      ok: false as const,
      error: withFixtureCode(error, 'INTERCEPTED_NETWORK'),
    }));

    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      interceptors: [{ onError: errorInterceptor }],
    });

    mockFetch.mockRejectedValue(new TypeError('Failed to fetch'));

    const result = await transport.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(false);
    expect(errorInterceptor).toHaveBeenCalled();
    if (!result.ok) expect(result.error.code).toBe('INTERCEPTED_NETWORK');
  });

  it('handles JSON parse error (line 146)', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => {
        throw new SyntaxError('Unexpected token');
      },
      text: async () => 'not valid json',
    });

    const result = await transport.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('PARSE_ERROR');
    }
  });

  it('handles non-JSON content type as text', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'text/plain' }),
      text: async () => 'plain text response',
    });

    const result = await transport.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.data).toBe('plain text response');
    }
  });

  it('handles 204 No Content', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 204,
      headers: new Headers(),
    });

    const result = await transport.request({ path: '/test', method: 'DELETE', headers: {} });
    expect(result.ok).toBe(true);
  });

  it('sets timeout abort controller when timeout specified (line 136)', async () => {
    vi.useFakeTimers();
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      timeout: 5000,
    });

    mockFetch.mockImplementation(async (_url, options) => {
      // Simulate the request being aborted
      const signal = options.signal;
      if (signal) {
        return new Promise<ResponseFixture>((_resolve, reject) => {
          signal.addEventListener('abort', () => {
            reject(new DOMException('The operation was aborted', 'AbortError'));
          });
        });
      }
      return { ok: true, status: 200, headers: new Headers() };
    });

    const resultPromise = transport.request({ path: '/test', method: 'GET', headers: {} });
    vi.advanceTimersByTime(6000);
    const result = await resultPromise;

    expect(result.ok).toBe(false);
    vi.useRealTimers();
  });

  it('builds URL with query params, filtering undefined/null', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({}),
    });

    await transport.request({
      path: '/test',
      method: 'GET',
      headers: {},
      params: { a: '1', b: undefined, c: 'yes' },
    });

    const calledUrl = mockFetch.mock.calls[0][0];
    expect(calledUrl).toContain('a=1');
    expect(calledUrl).toContain('c=yes');
    expect(calledUrl).not.toContain('b=');
  });

  it('adds X-Request-Id header when requestId is present', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({}),
    });

    await transport.request({
      path: '/test',
      method: 'GET',
      headers: {},
      requestId: 'test-id-123',
    });

    const calledOptions = mockFetch.mock.calls[0][1];
    const headers = calledOptions.headers;
    expect(headers.get('X-Request-Id')).toBe('test-id-123');
  });

  it('adds JSON body for POST requests', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({}),
    });

    await transport.request({
      path: '/test',
      method: 'POST',
      headers: {},
      body: { name: 'test' },
    });

    const calledOptions = mockFetch.mock.calls[0][1];
    expect(calledOptions.body).toBe(JSON.stringify({ name: 'test' }));
  });

  it('copies metricsKey to error on non-ok response', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      headers: new Headers(),
      json: async () => ({ message: 'Bad request' }),
      text: async () => 'Bad request',
    });

    const request: MetricsRequestConfig = {
      path: '/test',
      method: 'GET',
      headers: {},
      _metricsKey: 'my-metric',
    };
    const result = await transport.request(request);

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toHaveProperty('_metricsKey', 'my-metric');
  });

  it('copies metricsKey to success response', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ data: 'ok' }),
    });

    const request: MetricsRequestConfig = {
      path: '/test',
      method: 'GET',
      headers: {},
      _metricsKey: 'my-metric',
    };
    const result = await transport.request(request);

    expect(result.ok).toBe(true);
    if (result.ok) expect(result.data).toHaveProperty('_metricsKey', 'my-metric');
  });

  it('uses fallback UUID generator when crypto.randomUUID is unavailable', async () => {
    // Temporarily remove crypto.randomUUID to test fallback
    const origCrypto = globalThis.crypto;
    const mockCrypto: Partial<Crypto> = { ...origCrypto };
    delete mockCrypto.randomUUID;
    vi.stubGlobal('crypto', mockCrypto);

    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      generateRequestId: true,
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({}),
    });

    await transport.request({ path: '/test', method: 'GET', headers: {} });

    const calledOptions = mockFetch.mock.calls[0][1];
    const requestIdHeader = calledOptions.headers.get('X-Request-Id');
    expect(requestIdHeader).toBeTruthy();
    // Should be UUID-like format
    expect(requestIdHeader).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);

    vi.stubGlobal('crypto', origCrypto);
  });

  it('disables requestId generation when generateRequestId=false', async () => {
    const transport = createFetchTransport({
      baseUrl: 'http://localhost',
      generateRequestId: false,
    });

    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({}),
    });

    await transport.request({ path: '/test', method: 'GET', headers: {} });

    const calledOptions = mockFetch.mock.calls[0][1];
    const requestIdHeader = calledOptions.headers.get('X-Request-Id');
    expect(requestIdHeader).toBeNull();
  });
});
