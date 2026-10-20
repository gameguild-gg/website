/**
 * Extended Logging Interceptor Tests
 *
 * Test createLoggingInterceptor log levels, body logging,
 * header redaction, and onError behavior.
 */

import { describe, it, expect, vi } from 'vitest';
import { createLoggingInterceptor } from '../../src/plugins/logging.js';
import type { RequestConfig, ApiResponse } from '../../src/runtime/transport/types.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import { legacyCodeError } from '../support/contract-fixtures.js';

describe('createLoggingInterceptor', () => {
  describe('onRequest', () => {
    it('should log at info level by default', async () => {
      const logs: Array<{ level: string; message: string }> = [];
      const logger = (level: string, message: string) => {
        logs.push({ level, message });
      };

      const interceptor = createLoggingInterceptor({ logger });

      await interceptor.onRequest!({
        method: 'GET',
        path: '/api/users',
      });

      expect(logs).toHaveLength(1);
      expect(logs[0]!.level).toBe('info');
      expect(logs[0]!.message).toContain('GET');
      expect(logs[0]!.message).toContain('/api/users');
    });

    it('should log detailed info at debug level', async () => {
      const logs: Array<{ level: string; message: string; data?: unknown }> = [];
      const logger = (level: string, message: string, data?: unknown) => {
        logs.push({ level, message, data });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'debug',
        logRequestBody: true,
      });

      await interceptor.onRequest!({
        method: 'POST',
        path: '/api/users',
        body: { name: 'Test' },
        headers: { 'content-type': 'application/json' },
      });

      expect(logs).toHaveLength(1);
      expect(logs[0]!.level).toBe('debug');
      expect(logs[0]!.data).toBeDefined();
      const data = logs[0]!.data as Record<string, unknown>;
      expect(data.body).toEqual({ name: 'Test' });
    });

    it('should not log body when logRequestBody is false', async () => {
      const logs: Array<{ data?: unknown }> = [];
      const logger = (_level: string, _message: string, data?: unknown) => {
        logs.push({ data });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'debug',
        logRequestBody: false,
      });

      await interceptor.onRequest!({
        method: 'POST',
        path: '/api/users',
        body: { name: 'Secret' },
      });

      const data = logs[0]!.data as Record<string, unknown>;
      expect(data.body).toBeUndefined();
    });

    it('should suppress logging when level is higher than log', async () => {
      const logs: string[] = [];
      const logger = (level: string) => {
        logs.push(level);
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'error', // Only log error+
      });

      await interceptor.onRequest!({ method: 'GET', path: '/test' });

      // info/debug are below error, should not log
      expect(logs).toHaveLength(0);
    });

    it('should redact default sensitive headers', async () => {
      const logs: Array<{ data?: unknown }> = [];
      const logger = (_level: string, _message: string, data?: unknown) => {
        logs.push({ data });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'debug',
      });

      await interceptor.onRequest!({
        method: 'GET',
        path: '/test',
        headers: {
          authorization: 'Bearer secret',
          cookie: 'session=abc',
          'x-custom': 'visible',
        },
      });

      const data = logs[0]!.data as Record<string, unknown>;
      const headers = data.headers as Record<string, string>;
      expect(headers.authorization).toBe('[REDACTED]');
      expect(headers.cookie).toBe('[REDACTED]');
      expect(headers['x-custom']).toBe('visible');
    });

    it('should include request ID when available', async () => {
      const logs: Array<{ message: string }> = [];
      const logger = (_level: string, message: string) => {
        logs.push({ message });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        includeRequestId: true,
      });

      const request = {
        method: 'GET',
        path: '/test',
        _requestId: 'req-12345678-abcd',
      } satisfies RequestConfig & { _requestId: string };
      await interceptor.onRequest!(request);

      expect(logs[0]!.message).toContain('req-1234');
    });
  });

  describe('onResponse', () => {
    it('should log response status', async () => {
      const logs: Array<{ level: string; message: string }> = [];
      const logger = (level: string, message: string) => {
        logs.push({ level, message });
      };

      const interceptor = createLoggingInterceptor({ logger });

      const response: ApiResponse<unknown> = {
        data: { id: 1 },
        status: 200,
        headers: new Headers(),
      };

      await interceptor.onResponse!(response);

      expect(logs).toHaveLength(1);
      expect(logs[0]!.message).toContain('200');
    });

    it('should log response body at debug level when enabled', async () => {
      const logs: Array<{ data?: unknown }> = [];
      const logger = (_level: string, _message: string, data?: unknown) => {
        logs.push({ data });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'debug',
        logResponseBody: true,
      });

      const response: ApiResponse<unknown> = {
        data: { secret: 'payload' },
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
      };

      await interceptor.onResponse!(response);

      const data = logs[0]!.data as Record<string, unknown>;
      expect(data.data).toEqual({ secret: 'payload' });
    });

    it('should redact response headers', async () => {
      const logs: Array<{ data?: unknown }> = [];
      const logger = (_level: string, _message: string, data?: unknown) => {
        logs.push({ data });
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'debug',
      });

      const headers = new Headers({
        'set-cookie': 'session=secret',
        'content-type': 'application/json',
      });

      const response: ApiResponse<unknown> = {
        data: {},
        status: 200,
        headers,
      };

      await interceptor.onResponse!(response);

      const data = logs[0]!.data as Record<string, unknown>;
      const respHeaders = data.headers as Record<string, string>;
      expect(respHeaders['set-cookie']).toBe('[REDACTED]');
      expect(respHeaders['content-type']).toBe('application/json');
    });
  });

  describe('onError', () => {
    it('should log errors at warn level', async () => {
      const logs: Array<{ level: string; message: string; data?: unknown }> = [];
      const logger = (level: string, message: string, data?: unknown) => {
        logs.push({ level, message, data });
      };

      const interceptor = createLoggingInterceptor({ logger });

      const error: ApiError = {
        name: 'ApiError',
        message: 'Not found',
        status: 404,
        code: 'NOT_FOUND',
      };

      const result = await interceptor.onError!(error);

      expect(result.ok).toBe(false);
      expect(logs).toHaveLength(1);
      expect(logs[0]!.level).toBe('warn');
      expect(logs[0]!.message).toContain('Not found');
    });

    it('should include error details in log data', async () => {
      const logs: Array<{ data?: unknown }> = [];
      const logger = (_level: string, _message: string, data?: unknown) => {
        logs.push({ data });
      };

      const interceptor = createLoggingInterceptor({ logger });

      const error: ApiError = legacyCodeError({
        name: 'ApiError',
        message: 'Error',
        status: 500,
        code: 'INTERNAL',
        detail: 'Something went wrong',
        traceId: 'trace-123',
      });

      await interceptor.onError!(error);

      const data = logs[0]!.data as Record<string, unknown>;
      expect(data.code).toBe('INTERNAL');
      expect(data.status).toBe(500);
      expect(data.detail).toBe('Something went wrong');
      expect(data.traceId).toBe('trace-123');
    });

    it('should not log errors when level is above warn', async () => {
      const logs: string[] = [];
      const logger = (level: string) => {
        logs.push(level);
      };

      const interceptor = createLoggingInterceptor({
        logger,
        level: 'error', // Only error+
      });

      const error: ApiError = {
        name: 'ApiError',
        message: 'Not found',
        status: 404,
        code: 'NOT_FOUND',
      };

      await interceptor.onError!(error);

      // Warn is below error level, should not log
      expect(logs).toHaveLength(0);
    });

    it('should return error result', async () => {
      const interceptor = createLoggingInterceptor({
        logger: () => {},
      });

      const error: ApiError = legacyCodeError({
        name: 'ApiError',
        message: 'Error',
        status: 500,
        code: 'INTERNAL',
      });

      const result = await interceptor.onError!(error);

      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.error).toEqual(error);
      }
    });
  });

  describe('default logger', () => {
    it('should use console when no logger provided', async () => {
      const spy = vi.spyOn(console, 'info').mockImplementation(() => {});

      const interceptor = createLoggingInterceptor();

      await interceptor.onRequest!({ method: 'GET', path: '/test' });

      expect(spy).toHaveBeenCalled();
      spy.mockRestore();
    });
  });
});
