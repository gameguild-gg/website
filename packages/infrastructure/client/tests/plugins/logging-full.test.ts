/**
 * Logging plugin — cover defaultLogger switch branches and level filtering
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import { createLoggingInterceptor } from '../../src/plugins/logging.js';
import type { ApiResponse, RequestConfig } from '../../src/runtime/transport/types.js';

describe('createLoggingInterceptor — full branch', () => {
  afterEach(() => vi.restoreAllMocks());

  it('defaultLogger hits the debug branch', async () => {
    const spy = vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'debug' });

    await interceptor.onRequest!({
      path: '/test',
      method: 'GET',
      headers: {},
    });

    expect(spy).toHaveBeenCalled();
  });

  it('defaultLogger hits the warn branch via onError', async () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'warn' });

    await interceptor.onError!({
      name: 'ApiError',
      code: 'SERVER_ERROR',
      message: 'Something broke',
      status: 500,
    });

    expect(spy).toHaveBeenCalled();
  });

  it('defaultLogger hits the debug branch via debug level', async () => {
    const spy = vi.spyOn(console, 'debug').mockImplementation(() => {});
    // At debug level, onRequest calls logger('debug', ...) which calls console.debug
    const interceptor = createLoggingInterceptor({
      level: 'debug',
      logger: undefined, // uses defaultLogger
    });

    await interceptor.onRequest!({ path: '/test', method: 'POST', headers: {} });
    expect(console.debug).toHaveBeenCalled();
  });

  it('onRequest at info level skips debug and logs info', async () => {
    const spy = vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'info' });

    await interceptor.onRequest!({
      path: '/test',
      method: 'GET',
      headers: {},
    });

    expect(console.info).toHaveBeenCalled();
    expect(console.debug).not.toHaveBeenCalled();
  });

  it('onResponse at info level skips debug and logs info', async () => {
    const spy = vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'info' });

    const response: ApiResponse<unknown> = {
      data: { ok: true },
      status: 200,
      headers: new Headers(),
    };

    await interceptor.onResponse!(response);

    // info for response status
    expect(console.info).toHaveBeenCalled();
    expect(console.debug).not.toHaveBeenCalled();
  });

  it('onResponse at debug level logs debug with headers and body', async () => {
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({
      level: 'debug',
      logResponseBody: true,
    });

    const headers = new Headers({ 'Content-Type': 'application/json' });
    const response: ApiResponse<unknown> = {
      data: { id: 1 },
      status: 200,
      headers,
    };

    await interceptor.onResponse!(response);
    expect(console.debug).toHaveBeenCalled();
  });

  it('onRequest at debug level logs request body when enabled', async () => {
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({
      level: 'debug',
      logRequestBody: true,
    });

    await interceptor.onRequest!({
      path: '/test',
      method: 'POST',
      headers: {},
      body: { name: 'test' },
    });

    expect(console.debug).toHaveBeenCalledWith(
      expect.any(String),
      expect.stringContaining('POST'),
      expect.objectContaining({ body: { name: 'test' } })
    );
  });

  it('level warn suppresses info/debug for onRequest', async () => {
    vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'warn' });

    await interceptor.onRequest!({ path: '/test', method: 'GET', headers: {} });

    expect(console.info).not.toHaveBeenCalled();
    expect(console.debug).not.toHaveBeenCalled();
  });

  it('level warn suppresses info/debug for onResponse', async () => {
    vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'debug').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'warn' });

    await interceptor.onResponse!({
      data: null,
      status: 200,
      headers: new Headers(),
    });

    expect(console.info).not.toHaveBeenCalled();
    expect(console.debug).not.toHaveBeenCalled();
  });

  it('level error suppresses warn for onError', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    const interceptor = createLoggingInterceptor({ level: 'error' });

    await interceptor.onError!({
      name: 'ApiError',
      code: 'SERVER_ERROR',
      message: 'error',
      status: 500,
    });

    expect(console.warn).not.toHaveBeenCalled();
  });

  it('includeRequestId false omits request ID prefix', async () => {
    const calls: string[] = [];
    const interceptor = createLoggingInterceptor({
      level: 'info',
      includeRequestId: false,
      logger: (_level, message) => { calls.push(message); },
    });

    await interceptor.onRequest!({
      path: '/test',
      method: 'GET',
      headers: {},
      _requestId: 'req-123',
    } satisfies RequestConfig & { _requestId: string });

    expect(calls[0]).not.toContain('req-123');
  });
});
