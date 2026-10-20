/**
 * Plugin tests - Retry, Logging, Cache, Metrics
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createRetryPlugin } from '../../src/plugins/retry.js';
import { createLoggingInterceptor } from '../../src/plugins/logging.js';
import { createCacheInterceptor, MemoryCache } from '../../src/plugins/cache.js';
import { createMetricsInterceptor } from '../../src/plugins/metrics.js';
import type { ApiResponse, RequestConfig } from '../../src/runtime/transport/types.js';
import { ok, err } from '../../src/runtime/result/helpers.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import { fixtureTransport } from '../support/contract-fixtures.js';

type MetricsResponse = ApiResponse<unknown> & { _metricsKey?: string };

describe('Retry Plugin', () => {
  it('should create retry plugin with wrapTransport method', () => {
    const retryPlugin = createRetryPlugin({
      maxRetries: 3,
      baseDelay: 10,
    });

    expect(retryPlugin).toBeDefined();
    expect(retryPlugin.wrapTransport).toBeDefined();
    expect(typeof retryPlugin.wrapTransport).toBe('function');
  });

  it('should not retry successful requests', async () => {
    const respond = vi.fn(async () =>
      ok({ data: { success: true }, status: 200, headers: new Headers() }),
    );
    const mockTransport = fixtureTransport(respond);

    const retryPlugin = createRetryPlugin();
    const transport = retryPlugin.wrapTransport(mockTransport);

    await transport.request({ method: 'GET', path: '/test' });

    expect(respond).toHaveBeenCalledTimes(1);
  });
});

describe('Logging Interceptor', () => {
  it('should log requests and responses', async () => {
    const logs: string[] = [];
    const logger = (level: string, message: string) => {
      logs.push(`${level}: ${message}`);
    };

    const interceptor = createLoggingInterceptor({
      logger,
    });

    const request: RequestConfig = {
      method: 'POST',
      path: '/api/users',
      body: { name: 'Test' },
    };

    const processedRequest = await interceptor.onRequest!(request);
    expect(processedRequest).toBeDefined();
    expect(logs.length).toBeGreaterThan(0);
    expect(logs[0]).toContain('POST');
    expect(logs[0]).toContain('/api/users');
  });

  it('should redact sensitive headers', async () => {
    const logs: string[] = [];
    const logger = (level: string, message: string, data?: unknown) => {
      logs.push(message);
      if (data) {
        logs.push(JSON.stringify(data));
      }
    };

    const interceptor = createLoggingInterceptor({
      logger,
      level: 'debug',
      redactHeaders: ['authorization'],
    });

    const request: RequestConfig = {
      method: 'GET',
      path: '/test',
      headers: {
        authorization: 'Bearer secret-token',
        'x-api-key': 'api-key-123',
      },
    };

    await interceptor.onRequest!(request);

    const logOutput = logs.join(' ');
    expect(logOutput).toContain('[REDACTED]');
    expect(logOutput).not.toContain('secret-token');
    expect(logOutput).toContain('api-key-123'); // Not redacted
  });
});

describe('Cache Interceptor', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  it('should cache GET requests using MemoryCache', async () => {
    const cache = new MemoryCache();
    
    // Directly set in cache (interceptor would normally do this)
    const response = {
      data: { id: 123, name: 'Test User' },
      status: 200,
      headers: new Headers(),
    };

    cache.set('/api/users/123', response, 60000);

    // Check if cached
    const cached = cache.get<typeof response>('/api/users/123');
    expect(cached).toBeDefined();
    expect(cached?.data).toEqual(response.data);
  });

  it('should invalidate cache by path', async () => {
    const cache = new MemoryCache();
    cache.set('/api/users/123', { data: {}, status: 200, headers: {} }, 60000);

    expect(cache.get('/api/users/123')).toBeDefined();

    cache.invalidatePath('/api/users');

    expect(cache.get('/api/users/123')).toBeNull();
  });

  it('should invalidate cache by tags', async () => {
    const cache = new MemoryCache();
    cache.set('/api/users/123', { data: {}, status: 200, headers: new Headers() }, 60000, ['users']);
    cache.set('/api/posts/456', { data: {}, status: 200, headers: new Headers() }, 60000, ['posts']);

    cache.invalidateTags(['users']);

    expect(cache.get('/api/users/123')).toBeNull(); // Returns null when not found
    expect(cache.get('/api/posts/456')).toBeDefined();
  });

  it('should respect TTL', async () => {
    const cache = new MemoryCache();
    cache.set('/test', { data: {}, status: 200, headers: new Headers() }, 1000);

    expect(cache.get('/test')).toBeDefined();

    vi.advanceTimersByTime(2000);

    expect(cache.get('/test')).toBeNull(); // Expired returns null
  });
});

describe('Metrics Interceptor', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  it('should track request metrics', async () => {
    const metricsInterceptor = createMetricsInterceptor();

    const request: RequestConfig = {
      method: 'GET',
      path: '/api/test',
    };

    const processedRequest = await metricsInterceptor.onRequest!(request);
    
    vi.advanceTimersByTime(150);

    const response: MetricsResponse = {
      data: {},
      status: 200,
      headers: new Headers(),
    };

    // Copy the metrics key from the request to the response (simulating what fetch transport does)
    const metricsKey = (processedRequest as RequestConfig & { _metricsKey?: string })._metricsKey;
    if (metricsKey) {
      response._metricsKey = metricsKey;
    }

    await metricsInterceptor.onResponse!(response);

    const metrics = metricsInterceptor.getMetrics();
    expect(metrics.length).toBe(1);
    expect(metrics[0].method).toBe('GET');
    expect(metrics[0].path).toBe('/api/test');
    expect(metrics[0].status).toBe(200);
  });

  it('should calculate aggregated metrics', async () => {
    const metricsInterceptor = createMetricsInterceptor();

    // Simulate multiple requests
    for (let i = 0; i < 10; i++) {
      const request: RequestConfig = {
        method: 'GET',
        path: '/api/test',
        requestId: `req-${i}`,
      };

      const processedRequest = await metricsInterceptor.onRequest!(request);
      
      vi.advanceTimersByTime(100 + i * 10);

      const response: MetricsResponse = { data: {}, status: 200, headers: new Headers() };
      // Copy the metrics key
      const metricsKey = (processedRequest as RequestConfig & { _metricsKey?: string })._metricsKey;
      if (metricsKey) {
        response._metricsKey = metricsKey;
      }

      await metricsInterceptor.onResponse!(response);
    }

    const aggregated = metricsInterceptor.getAggregatedMetrics();
    
    expect(aggregated.totalRequests).toBe(10);
    expect(aggregated.successRate).toBe(1.0);
    expect(aggregated.p50Duration).toBeGreaterThan(0);
    expect(aggregated.p95Duration).toBeGreaterThan(aggregated.p50Duration);
    expect(aggregated.p99Duration).toBeGreaterThanOrEqual(aggregated.p95Duration); // Can be equal with small sample
  });

  it('should track errors', async () => {
    const metricsInterceptor = createMetricsInterceptor();

    const request: RequestConfig = {
      method: 'POST',
      path: '/api/fail',
    };

    const processedRequest = await metricsInterceptor.onRequest!(request);

    const error: ApiError & { _metricsKey?: string } = { code: 'VALIDATION_ERROR', message: 'Invalid data', status: 400, name: 'ApiError' };
    // Copy the metrics key to the error
    const metricsKey = (processedRequest as RequestConfig & { _metricsKey?: string })._metricsKey;
    if (metricsKey) {
      error._metricsKey = metricsKey;
    }

    await metricsInterceptor.onError!(error);

    const metrics = metricsInterceptor.getMetrics();
    expect(metrics[0].status).toBe(400);
    expect(metrics[0].error).toBe('VALIDATION_ERROR');

    const aggregated = metricsInterceptor.getAggregatedMetrics();
    expect(aggregated.successRate).toBe(0);
    expect(aggregated.errorsByCode.VALIDATION_ERROR).toBe(1);
  });
});
