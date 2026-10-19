/**
 * Extended Cache Tests — createCacheInterceptor onRequest/onResponse, defaultGenerateTags
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createCacheInterceptor } from '../../src/plugins/cache.js';
import type { ApiResponse, RequestConfig } from '../../src/runtime/transport/types.js';

type CachedRequest = RequestConfig & { _cachedData?: unknown };
type CacheResponse = ApiResponse<unknown> & { _request?: RequestConfig };

describe('createCacheInterceptor', () => {
  let interceptor: ReturnType<typeof createCacheInterceptor>;

  beforeEach(() => {
    interceptor = createCacheInterceptor({ defaultTtl: 5000, maxEntries: 50 });
  });

  describe('onRequest — cache miss', () => {
    it('should pass through request when not cached', async () => {
      const request: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
      };

      const result = await interceptor.onRequest!(request);
      expect(result).toEqual(request);
      expect((result as CachedRequest)._cachedData).toBeUndefined();
    });

    it('should skip non-cacheable methods', async () => {
      const request: RequestConfig = {
        method: 'POST',
        path: '/users',
        headers: {},
      };

      const result = await interceptor.onRequest!(request);
      expect((result as CachedRequest)._cachedData).toBeUndefined();
    });
  });

  describe('onRequest — cache hit', () => {
    it('should attach cached data to request', async () => {
      // First, put data in cache
      interceptor.cache.set('GET:/users:', { users: ['a'] }, 5000, ['users']);

      const request: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
      };

      const result = await interceptor.onRequest!(request);
      expect((result as CachedRequest)._cachedData).toEqual({ users: ['a'] });
    });
  });

  describe('onResponse — caching', () => {
    it('should cache successful GET responses', async () => {
      const request: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
      };

      const response: CacheResponse = {
        data: { users: ['user1'] },
        status: 200,
        headers: new Headers(),
        _request: request, // Linked request for key generation
      };

      const result = await interceptor.onResponse!(response);
      expect(result).toBe(response);

      // Data should now be cached
      const cached = interceptor.cache.get('GET:/users:');
      expect(cached).toEqual({ users: ['user1'] });
    });

    it('should not cache error responses', async () => {
      const request: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
      };

      const response: CacheResponse = {
        data: { error: 'Internal' },
        status: 500,
        headers: new Headers(),
        _request: request,
      };

      await interceptor.onResponse!(response);

      const cached = interceptor.cache.get('GET:/users:');
      expect(cached).toBeNull();
    });

    it('should not cache when _request is missing', async () => {
      const response: CacheResponse = {
        data: { test: true },
        status: 200,
        headers: new Headers(),
      };

      await interceptor.onResponse!(response);
      expect(interceptor.cache.stats().size).toBe(0);
    });

    it('should not cache non-GET methods even with _request', async () => {
      const request: RequestConfig = {
        method: 'POST',
        path: '/users',
        headers: {},
      };

      const response: CacheResponse = {
        data: { id: '1' },
        status: 201,
        headers: new Headers(),
        _request: request,
      };

      await interceptor.onResponse!(response);
      expect(interceptor.cache.stats().size).toBe(0);
    });
  });

  describe('custom cacheMethods', () => {
    it('should cache HEAD requests when configured', async () => {
      const custom = createCacheInterceptor({ cacheMethods: ['GET', 'HEAD'] });

      const request: RequestConfig = {
        method: 'HEAD',
        path: '/healthz',
        headers: {},
      };

      await custom.onRequest!(request);

      const response: CacheResponse = {
        data: null,
        status: 200,
        headers: new Headers(),
        _request: request,
      };

      await custom.onResponse!(response);
      expect(custom.cache.stats().size).toBe(1);
    });
  });

  describe('cache key with params', () => {
    it('should generate different keys for different params', async () => {
      const req1: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
        params: { page: 1 },
      };

      const req2: RequestConfig = {
        method: 'GET',
        path: '/users',
        headers: {},
        params: { page: 2 },
      };

      // Cache both
      interceptor.cache.set('GET:/users:{"page":1}', 'page1', 5000, []);
      interceptor.cache.set('GET:/users:{"page":2}', 'page2', 5000, []);

      const result1 = await interceptor.onRequest!(req1);
      expect((result1 as CachedRequest)._cachedData).toBe('page1');

      const result2 = await interceptor.onRequest!(req2);
      expect((result2 as CachedRequest)._cachedData).toBe('page2');
    });
  });
});

describe('defaultGenerateTags — via interceptor', () => {
  it('should generate tags from simple REST path', async () => {
    const interceptor = createCacheInterceptor();

    const request: RequestConfig = {
      method: 'GET',
      path: '/users',
      headers: {},
    };

    const response: CacheResponse = {
      data: [],
      status: 200,
      headers: new Headers(),
      _request: request,
    };

    await interceptor.onResponse!(response);

    // 'users' tag should exist
    expect(interceptor.cache.stats().size).toBe(1);

    // Test tag invalidation
    const deleted = interceptor.cache.invalidateByTag('users');
    expect(deleted).toBe(1);
  });

  it('should generate entity:id tags for UUID paths', async () => {
    const interceptor = createCacheInterceptor();

    const request: RequestConfig = {
      method: 'GET',
      path: '/users/a1b2c3d4-e5f6-7890-abcd-ef1234567890',
      headers: {},
    };

    const response: CacheResponse = {
      data: { id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890' },
      status: 200,
      headers: new Headers(),
      _request: request,
    };

    await interceptor.onResponse!(response);

    // Should have users + users:uuid tag
    const deleted = interceptor.cache.invalidateByTag('users:a1b2c3d4-e5f6-7890-abcd-ef1234567890');
    expect(deleted).toBe(1);
  });

  it('should skip version prefixes in paths', async () => {
    const interceptor = createCacheInterceptor();

    const request: RequestConfig = {
      method: 'GET',
      path: '/v1/projects/abc12345-6789-0def-abcd-111222333444',
      headers: {},
    };

    const response: CacheResponse = {
      data: {},
      status: 200,
      headers: new Headers(),
      _request: request,
    };

    await interceptor.onResponse!(response);

    // v1 should be skipped, should have 'projects' and 'projects:abc...' tags
    const deleted1 = interceptor.cache.invalidateByTag('projects');
    expect(deleted1).toBe(1);
  });

  it('should use custom generateTags when provided', async () => {
    const customTags = vi.fn(() => ['custom', 'tag']);
    const interceptor = createCacheInterceptor({ generateTags: customTags });

    const request: RequestConfig = {
      method: 'GET',
      path: '/anything',
      headers: {},
    };

    const response: CacheResponse = {
      data: {},
      status: 200,
      headers: new Headers(),
      _request: request,
    };

    await interceptor.onResponse!(response);
    expect(customTags).toHaveBeenCalledWith(request);
    expect(interceptor.cache.invalidateByTag('custom')).toBe(1);
  });
});
