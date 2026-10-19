/**
 * Extended DevTools Tests — method emojis, sanitization, log level filtering
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { DevTools } from '../../src/runtime/devtools/devtools.js';
import type { ApiError } from '../../src/runtime/errors/types.js';

type LegacyLoggedError = Partial<Omit<ApiError, 'code'>> & { code: ApiError['code'] | 'ERR' };

function legacyLoggedError(error: LegacyLoggedError): ApiError {
  // Preserve the partial and nonstandard error records used by these logging
  // cases. The logger prints them without reading ApiError-specific fields.
  return error as ApiError;
}

function loggedHeaders(value: unknown): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new TypeError('Expected the logged header object');
  }
  return value as Record<string, unknown>;
}

describe('DevTools — extended', () => {
  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'info').mockImplementation(() => {});
    vi.spyOn(console, 'group').mockImplementation(() => {});
    vi.spyOn(console, 'groupCollapsed').mockImplementation(() => {});
    vi.spyOn(console, 'groupEnd').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should create disabled DevTools', () => {
    const devtools = new DevTools({ enabled: false });
    devtools.logRequestStart({ path: '/test', method: 'GET' });
    // Silent log level suppresses info/warn/error/debug, but group() always calls console.groupCollapsed
    expect(console.info).not.toHaveBeenCalled();
  });

  it('should create enabled DevTools and log request', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logRequestStart({ path: '/test', method: 'GET', requestId: 'r1' });
    expect(console.groupCollapsed).toHaveBeenCalled();
    expect(console.info).toHaveBeenCalled();
  });

  it('should log request with params at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logRequestStart({
      path: '/api/users',
      method: 'GET',
      params: { page: '1' },
      requestId: 'r2',
    });
    expect(console.log).toHaveBeenCalled();
  });

  it('should log request with headers at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logRequestStart({
      path: '/api/users',
      method: 'GET',
      headers: { 'X-Custom': 'value' },
      requestId: 'r3',
    });
    expect(console.log).toHaveBeenCalled();
  });

  it('should log request with body at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logRequestStart({
      path: '/api/users',
      method: 'POST',
      body: { name: 'test' },
      requestId: 'r4',
    });
    expect(console.log).toHaveBeenCalled();
  });

  it('should log successful completion', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'info' });
    devtools.logRequestComplete(
      { path: '/test', method: 'GET', requestId: 'r5' },
      { ok: true, data: { result: 'ok' } },
      100
    );
    expect(console.info).toHaveBeenCalled();
    expect(console.groupEnd).toHaveBeenCalled();
  });

  it('should log error completion', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'error' });
    devtools.logRequestComplete(
      { path: '/test', method: 'GET', requestId: 'r6' },
      { ok: false, error: legacyLoggedError({ code: 'NOT_FOUND' }) }
    );
    expect(console.error).toHaveBeenCalled();
  });

  it('should calculate duration from tracked request times', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'info' });
    devtools.logRequestStart({ path: '/t', method: 'GET', requestId: 'r7' });
    devtools.logRequestComplete(
      { path: '/t', method: 'GET', requestId: 'r7' },
      { ok: true, data: null }
    );
    expect(console.info).toHaveBeenCalled();
  });

  it('should log validation error for request context', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'error' });
    devtools.logValidationError('request', new Error('Bad input'));
    expect(console.error).toHaveBeenCalled();
  });

  it('should log validation error for response context', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'error' });
    devtools.logValidationError('response', new Error('Bad output'));
    expect(console.error).toHaveBeenCalled();
  });

  it('should log cache hit at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logCacheHit('cache-key-1');
    expect(console.log).toHaveBeenCalled();
  });

  it('should log cache miss at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logCacheMiss('cache-key-2');
    expect(console.log).toHaveBeenCalled();
  });

  it('should log retry at warn level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'warn' });
    devtools.logRetry(1, 3, legacyLoggedError({ name: 'ApiError', code: 'NETWORK_ERROR', message: 'fail' }));
    expect(console.warn).toHaveBeenCalled();
  });

  it('should log deduplication at debug level', () => {
    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
    devtools.logDeduplication('dedup-key');
    expect(console.log).toHaveBeenCalled();
  });

  describe('method emojis', () => {
    it('should use 🔍 for GET', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'GET' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('🔍');
    });

    it('should use ✉️ for POST', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'POST' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('✉️');
    });

    it('should use 📝 for PUT', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'PUT' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('📝');
    });

    it('should use 🔧 for PATCH', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'PATCH' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('🔧');
    });

    it('should use 🗑️ for DELETE', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'DELETE' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('🗑️');
    });

    it('should use 🌐 for unknown methods', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'OPTIONS' });
      const call: unknown = vi.mocked(console.groupCollapsed).mock.calls[0]?.[0] || '';
      expect(call).toContain('🌐');
    });
  });

  describe('header sanitization', () => {
    it('should sanitize sensitive headers in debug logs', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'debug' });
      devtools.logRequestStart({
        path: '/api/data',
        method: 'GET',
        headers: {
          Authorization: 'Bearer secret-token',
          Cookie: 'session=abc',
          'X-Api-Key': 'api-key-123',
          'X-Auth-Token': 'auth-token',
          'Content-Type': 'application/json',
        },
        requestId: 'req-sanitize',
      });

      const logCalls: unknown[][] = vi.mocked(console.log).mock.calls;
      const headerCall = logCalls.find((c) => typeof c[0] === 'string' && c[0].includes('Headers'));
      expect(headerCall).toBeDefined();
      if (headerCall) {
        const headers = loggedHeaders(headerCall[1]);
        expect(headers.Authorization).toBe('[REDACTED]');
        expect(headers.Cookie).toBe('[REDACTED]');
        expect(headers['Content-Type']).toBe('application/json');
      }
    });
  });

  describe('custom logger', () => {
    it('should use custom logger when provided', () => {
      const customLogger = {
        log: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        info: vi.fn(),
        debug: vi.fn(),
        group: vi.fn(),
        groupEnd: vi.fn(),
      };

      const devtools = new DevTools({ enabled: true, logger: customLogger });
      devtools.logRequestStart({ path: '/', method: 'GET', requestId: 'r-custom' });

      expect(customLogger.group).toHaveBeenCalled();
    });
  });

  describe('collapsed option', () => {
    it('should use expanded groups when collapsed=false', () => {
      const devtools = new DevTools({ enabled: true, collapsed: false, logLevel: 'info' });
      devtools.logRequestStart({ path: '/', method: 'GET' });
      expect(console.group).toHaveBeenCalled();
    });
  });

  describe('log level filtering via ConsoleLogger', () => {
    it('should suppress warn at error level', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'error' });
      devtools.logRetry(1, 3, legacyLoggedError({ name: 'ApiError', code: 'ERR', message: '' }));
      expect(console.warn).not.toHaveBeenCalled();
    });

    it('should suppress error at silent level', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'silent' });
      devtools.logValidationError('request', new Error('test'));
      expect(console.error).not.toHaveBeenCalled();
    });

    it('should suppress info at warn level', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'warn' });
      devtools.logRequestComplete(
        { path: '/', method: 'GET', requestId: 'w' },
        { ok: true, data: null },
        10
      );
      expect(console.info).not.toHaveBeenCalled();
    });

    it('should suppress debug at info level', () => {
      const devtools = new DevTools({ enabled: true, logLevel: 'info' });
      devtools.logCacheHit('k');
      expect(console.log).not.toHaveBeenCalled();
    });
  });
});
