/**
 * Metrics plugin — cover onMetrics callback, maxMetrics shift, error onError
 */
import { describe, it, expect, vi } from 'vitest';
import { createMetricsInterceptor } from '../../src/plugins/metrics.js';
import type { ApiError } from '../../src/runtime/errors/types.js';
import type { ApiResponse, RequestConfig } from '../../src/runtime/transport/types.js';

type MetricsMetadata = { _metricsKey?: string; _requestId?: string };
type MetricsRequest = RequestConfig & MetricsMetadata;
type MetricsResponse<T> = ApiResponse<T> & MetricsMetadata;
type MetricsError = ApiError & MetricsMetadata;

describe('createMetricsInterceptor — full branch', () => {
  it('onMetrics callback fires on response', async () => {
    const onMetrics = vi.fn();
    const interceptor = createMetricsInterceptor({ onMetrics });

    // Track a request
    const request: MetricsRequest = await interceptor.onRequest!({
      path: '/api/test',
      method: 'GET',
      headers: {},
    });

    const metricsKey = request._metricsKey;
    expect(metricsKey).toBeDefined();

    // Process response with the metricsKey
    const response: MetricsResponse<{ ok: boolean }> = {
      data: { ok: true },
      status: 200,
      headers: new Headers(),
      _metricsKey: metricsKey,
    };

    await interceptor.onResponse!(response);

    expect(onMetrics).toHaveBeenCalledWith(
      expect.objectContaining({
        method: 'GET',
        path: '/api/test',
        status: 200,
        success: true,
      })
    );
  });

  it('maxMetrics evicts oldest when full', async () => {
    const interceptor = createMetricsInterceptor({ maxMetrics: 2 });

    // Add 3 metrics
    for (let i = 0; i < 3; i++) {
      const request: MetricsRequest = await interceptor.onRequest!({
        path: `/api/item${i}`,
        method: 'GET',
        headers: {},
      });
      const key = request._metricsKey;
      const response: MetricsResponse<null> = {
        data: null,
        status: 200,
        headers: new Headers(),
        _metricsKey: key,
      };
      await interceptor.onResponse!(response);
    }

    const metrics = interceptor.getMetrics();
    expect(metrics.length).toBe(2);
    // Oldest (item0) should be evicted
    expect(metrics[0].path).toBe('/api/item1');
    expect(metrics[1].path).toBe('/api/item2');
  });

  it('onError tracks error metrics with code', async () => {
    const onMetrics = vi.fn();
    const interceptor = createMetricsInterceptor({ onMetrics });

    const request: MetricsRequest = await interceptor.onRequest!({
      path: '/api/fail',
      method: 'POST',
      headers: {},
    });
    const metricsKey = request._metricsKey;

    const error: MetricsError = {
      name: 'ApiError',
      code: 'SERVER_ERROR',
      message: 'Internal error',
      status: 500,
      _metricsKey: metricsKey,
    };
    const result = await interceptor.onError!(error);

    expect(result.ok).toBe(false);
    expect(onMetrics).toHaveBeenCalledWith(
      expect.objectContaining({
        method: 'POST',
        path: '/api/fail',
        status: 500,
        success: false,
        error: 'SERVER_ERROR',
      })
    );
  });

  it('onError without metricsKey returns error unchanged', async () => {
    const interceptor = createMetricsInterceptor();

    const result = await interceptor.onError!({
      name: 'ApiError',
      code: 'NETWORK_ERROR',
      message: 'Disconnected',
      status: 0,
    });

    expect(result.ok).toBe(false);
    if (result.ok) throw new Error('Expected the original metrics error');
    expect(result.error.code).toBe('NETWORK_ERROR');
  });

  it('onResponse without metricsKey returns response unchanged', async () => {
    const interceptor = createMetricsInterceptor();

    const response: ApiResponse<{ ok: boolean }> = {
      data: { ok: true },
      status: 200,
      headers: new Headers(),
    };

    const result = await interceptor.onResponse!(response);
    expect(result.status).toBe(200);
  });

  it('onResponse with metricsKey but missing timing returns unchanged', async () => {
    const interceptor = createMetricsInterceptor();

    const response: MetricsResponse<Record<string, never>> = {
      data: {},
      status: 200,
      headers: new Headers(),
      _metricsKey: 'nonexistent-key',
    };

    const result = await interceptor.onResponse!(response);
    expect(result.status).toBe(200);
  });

  it('onError evicts oldest when maxMetrics exceeded', async () => {
    const interceptor = createMetricsInterceptor({ maxMetrics: 1 });

    // First: successful request
    const req1: MetricsRequest = await interceptor.onRequest!({
      path: '/ok',
      method: 'GET',
      headers: {},
    });
    const response: MetricsResponse<null> = {
      data: null,
      status: 200,
      headers: new Headers(),
      _metricsKey: req1._metricsKey,
    };
    await interceptor.onResponse!(response);

    // Second: error
    const req2: MetricsRequest = await interceptor.onRequest!({
      path: '/fail',
      method: 'GET',
      headers: {},
    });
    const error: MetricsError = {
      name: 'ApiError',
      code: 'SERVER_ERROR',
      message: 'fail',
      status: 500,
      _metricsKey: req2._metricsKey,
    };
    await interceptor.onError!(error);

    const metrics = interceptor.getMetrics();
    expect(metrics.length).toBe(1);
    expect(metrics[0].path).toBe('/fail');
  });

  it('includeRequestId false omits requestId from metrics', async () => {
    const interceptor = createMetricsInterceptor({ includeRequestId: false });

    const requestConfig: MetricsRequest = {
      path: '/test',
      method: 'GET',
      headers: {},
      _requestId: 'req-xyz',
    };
    const request: MetricsRequest = await interceptor.onRequest!(requestConfig);

    const response: MetricsResponse<null> = {
      data: null,
      status: 200,
      headers: new Headers(),
      _metricsKey: request._metricsKey,
    };
    await interceptor.onResponse!(response);

    const metrics = interceptor.getMetrics();
    expect(metrics[0].requestId).toBeUndefined();
  });

  it('aggregatedMetrics empty returns zeros', () => {
    const interceptor = createMetricsInterceptor();
    const agg = interceptor.getAggregatedMetrics();
    expect(agg.totalRequests).toBe(0);
    expect(agg.successRate).toBe(0);
    expect(agg.averageDuration).toBe(0);
    expect(agg.p50Duration).toBe(0);
  });

  it('clearMetrics resets everything', async () => {
    const interceptor = createMetricsInterceptor();

    const req: MetricsRequest = await interceptor.onRequest!({
      path: '/test',
      method: 'GET',
      headers: {},
    });
    const response: MetricsResponse<null> = {
      data: null,
      status: 200,
      headers: new Headers(),
      _metricsKey: req._metricsKey,
    };
    await interceptor.onResponse!(response);

    expect(interceptor.getMetrics().length).toBe(1);
    interceptor.clearMetrics();
    expect(interceptor.getMetrics().length).toBe(0);
  });
});
