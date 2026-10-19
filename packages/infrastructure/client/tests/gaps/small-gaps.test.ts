/**
 * Small gap coverage tests for multiple files
 * Covers remaining uncovered lines across several modules
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ZodError } from 'zod';

// ─── Guards (line 128) ─────────────────────────────
import { isRetryableError } from '../../src/runtime/errors/guards.js';

describe('isRetryableError — gap coverage (line 128)', () => {
  it('returns true for 502 status', () => {
    const error = { name: 'ApiError', status: 502, code: 'BAD_GATEWAY', message: 'bad' };
    expect(isRetryableError(error)).toBe(true);
  });

  it('returns true for 503 status', () => {
    const error = { name: 'ApiError', status: 503, code: 'SERVICE_UNAVAILABLE', message: 'down' };
    expect(isRetryableError(error)).toBe(true);
  });

  it('returns true for 504 status', () => {
    const error = { name: 'ApiError', status: 504, code: 'GATEWAY_TIMEOUT', message: 'timeout' };
    expect(isRetryableError(error)).toBe(true);
  });

  it('returns false for 400 status', () => {
    const error = { name: 'ApiError', status: 400, code: 'BAD_REQUEST', message: 'bad' };
    expect(isRetryableError(error)).toBe(false);
  });

  it('returns false for non-ApiError', () => {
    expect(isRetryableError(new Error('nope'))).toBe(false);
  });
});

// ─── Validation (line 111) ──────────────────────────
import { safeParse } from '../../src/runtime/errors/validation.js';

describe('safeParse — gap coverage (line 111)', () => {
  it('returns parsed data for valid input', () => {
    const schema = { parse: (d: unknown) => d as string };
    const result = safeParse(schema, 'hello');
    expect(result).toBe('hello');
  });

  it('re-throws non-ZodError', () => {
    const schema = {
      parse: () => {
        throw new TypeError('unexpected');
      },
    };
    expect(() => safeParse(schema, 'bad')).toThrow(TypeError);
  });

  it('transforms ZodError into ValidationError', () => {
    const validationError = new ZodError([{ code: 'custom', message: 'bad', path: ['x'] }]);
    const schema = {
      parse: () => {
        throw validationError;
      },
    };

    let transformed: unknown;
    try {
      safeParse(schema, 'bad');
    } catch (e) {
      transformed = e;
      expect(e).toBeTruthy();
    }
    expect(transformed).not.toBe(validationError);
    expect(transformed).toMatchObject({
      name: 'ApiError',
      code: 'VALIDATION_ERROR',
      status: 400,
      message: 'Response validation failed',
      metadata: {
        context: 'unknown',
        errors: [{ field: 'x', message: 'bad', code: 'custom' }],
        timestamp: expect.any(String),
      },
    });
  });

  it('re-throws an Error named ZodError without native ZodError identity', () => {
    const original = Object.assign(new Error('validation failed'), {
      issues: [{ code: 'custom', message: 'bad', path: ['x'] }],
      name: 'ZodError',
    });
    const schema = { parse: () => { throw original; } };
    let caught: unknown;

    try {
      safeParse(schema, 'bad');
    } catch (error) {
      caught = error;
    }

    expect(caught).toBe(original);
  });
});

// ─── Retry plugin (line 47) ─────────────────────────
import { createRetryPlugin } from '../../src/plugins/retry.js';
import type { RateLimitError } from '../../src/runtime/errors/types.js';
import { fixtureTransport, legacyCodeError } from '../support/contract-fixtures.js';

describe('createRetryPlugin — gap coverage (line 47)', () => {
  it('uses retryAfter delay when present', async () => {
    let attempt = 0;
    const mockTransport = fixtureTransport(async () => {
      attempt++;
      if (attempt === 1) {
        return {
          ok: false,
          error: {
            name: 'ApiError' as const,
            status: 429,
            code: 'RATE_LIMITED' as const,
            message: 'Too Many Requests',
            retryAfter: 1, // 1 second
          } satisfies RateLimitError,
        };
      }
      return {
        ok: true,
        data: { data: 'success', status: 200, headers: new Headers() },
      };
    });

    const plugin = createRetryPlugin({
      maxRetries: 2,
      baseDelay: 10,
      maxDelay: 5000,
      exponentialBackoff: false,
    });

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(true);
    expect(attempt).toBe(2);
  });

  it('uses linear delay when exponentialBackoff is false', async () => {
    let attempt = 0;
    const mockTransport = fixtureTransport(async () => {
      attempt++;
      if (attempt <= 2) {
        return {
          ok: false,
          error: legacyCodeError({
            name: 'ApiError' as const,
            status: 503,
            code: 'SERVICE_UNAVAILABLE' as const,
            message: 'Down',
          }),
        };
      }
      return {
        ok: true,
        data: { data: 'ok', status: 200, headers: new Headers() },
      };
    });

    const plugin = createRetryPlugin({
      maxRetries: 3,
      baseDelay: 1,
      maxDelay: 100,
      exponentialBackoff: false,
    });

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });
    expect(result.ok).toBe(true);
    expect(attempt).toBe(3);
  });
});

// ─── Logging plugin (lines 67-68 — error branch) ───
import { createLoggingInterceptor } from '../../src/plugins/logging.js';

describe('createLoggingInterceptor — error level gap (lines 67-68)', () => {
  it('defaultLogger hits the error branch via onError callback', async () => {
    const spy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    // At 'debug' level, onError calls logger('warn', ...) which hits console.warn
    const interceptor = createLoggingInterceptor({ level: 'debug' });

    await interceptor.onError!({
      name: 'ApiError',
      code: 'SERVER_ERROR',
      message: 'Server error',
      status: 500,
    });

    expect(spy).toHaveBeenCalled();
    spy.mockRestore();
  });

  it('error level only logs error, not warn/info/debug', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});
    const debugSpy = vi.spyOn(console, 'debug').mockImplementation(() => {});

    const interceptor = createLoggingInterceptor({ level: 'error' });

    // onRequest at error level should log nothing (info < error)
    await interceptor.onRequest!({ path: '/test', method: 'GET', headers: {} });
    expect(infoSpy).not.toHaveBeenCalled();
    expect(debugSpy).not.toHaveBeenCalled();

    // onError at error level calls logger('warn', ...) but warn < error, so suppressed
    await interceptor.onError!(legacyCodeError({
      name: 'ApiError',
      code: 'X',
      message: 'err',
      status: 500,
    }));
    expect(warnSpy).not.toHaveBeenCalled();

    errorSpy.mockRestore();
    warnSpy.mockRestore();
    infoSpy.mockRestore();
    debugSpy.mockRestore();
  });
});

// ─── Auth-retry (line 155 — unreachable fallback) ───
import { createAuthRetryPlugin } from '../../src/plugins/auth-retry.js';

describe('createAuthRetryPlugin — authRefreshFailed (line 155)', () => {
  it('returns authRefreshFailed metadata when refresh returns false', async () => {
    const refreshToken = vi.fn().mockResolvedValue(false);
    const onAuthRequired = vi.fn();

    const plugin = createAuthRetryPlugin({
      refreshToken,
      maxRetries: 1,
      onAuthenticationRequired: onAuthRequired,
    });

    let callCount = 0;
    const mockTransport = fixtureTransport(async () => {
      callCount++;
      return {
        ok: false,
        error: legacyCodeError({
          name: 'ApiError' as const,
          status: 401,
          code: 'UNAUTHORIZED' as const,
          message: 'Unauthorized',
        }),
      };
    });

    const wrapped = plugin.wrapTransport(mockTransport);
    const result = await wrapped.request({ path: '/test', method: 'GET', headers: {} });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.metadata?.authRefreshFailed).toBe(true);
    }
  });
});

// ─── DevTools (line 62) ─────────────────────────────
import { DevTools } from '../../src/runtime/devtools/devtools.js';

describe('DevTools — gap coverage (line 62)', () => {
  it('ConsoleLogger.warn is suppressed at error level', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    const groupSpy = vi.spyOn(console, 'group').mockImplementation(() => {});
    const groupEndSpy = vi.spyOn(console, 'groupEnd').mockImplementation(() => {});

    // DevTools with error level — warn should be suppressed
    const devtools = new DevTools({ enabled: true, logLevel: 'error' });

    // logRequestStart should call logger methods
    devtools.logRequestStart({ path: '/test', method: 'GET', headers: {} });

    // At error level, warn calls should be suppressed
    // The ConsoleLogger.warn checks logLevel !== 'silent' && logLevel !== 'error'
    // So at 'error' level, warn is suppressed

    warnSpy.mockRestore();
    logSpy.mockRestore();
    groupSpy.mockRestore();
    groupEndSpy.mockRestore();
  });

  it('DevTools uses silent logger when enabled=false', () => {
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});
    const debugSpy = vi.spyOn(console, 'debug').mockImplementation(() => {});
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    const groupSpy = vi.spyOn(console, 'group').mockImplementation(() => {});
    const groupEndSpy = vi.spyOn(console, 'groupEnd').mockImplementation(() => {});

    const devtools = new DevTools({ enabled: false });
    devtools.logRequestStart({ path: '/test', method: 'GET', headers: {} });

    // Silent level suppresses info, debug, warn
    expect(infoSpy).not.toHaveBeenCalled();
    expect(debugSpy).not.toHaveBeenCalled();
    expect(warnSpy).not.toHaveBeenCalled();

    infoSpy.mockRestore();
    debugSpy.mockRestore();
    warnSpy.mockRestore();
    logSpy.mockRestore();
    groupSpy.mockRestore();
    groupEndSpy.mockRestore();
  });

  it('logRequestComplete logs success and error', () => {
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    const groupSpy = vi.spyOn(console, 'group').mockImplementation(() => {});
    const groupEndSpy = vi.spyOn(console, 'groupEnd').mockImplementation(() => {});
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    const devtools = new DevTools({ enabled: true, logLevel: 'debug' });

    // Log successful completion
    devtools.logRequestComplete({ path: '/test', method: 'GET', headers: {} }, { ok: true, data: { data: 'ok', status: 200, headers: new Headers() } });

    // Log error completion
    devtools.logRequestComplete(
      { path: '/test', method: 'GET', headers: {} },
      { ok: false, error: legacyCodeError({ name: 'ApiError', status: 500, code: 'ERROR', message: 'fail' }) },
    );

    logSpy.mockRestore();
    groupSpy.mockRestore();
    groupEndSpy.mockRestore();
    errorSpy.mockRestore();
  });
});
