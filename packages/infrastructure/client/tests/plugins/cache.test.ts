/**
 * Extended Cache Plugin Tests
 *
 * Covers MemoryCache LRU eviction, invalidation methods,
 * and the createCacheInterceptor factory.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { MemoryCache, createCacheInterceptor } from '../../src/plugins/cache.js';
import type { RequestConfig } from '../../src/runtime/transport/types.js';

describe('MemoryCache', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('get/set', () => {
    it('should store and retrieve values', () => {
      const cache = new MemoryCache();
      cache.set('key1', { data: 'hello' }, 60000);

      expect(cache.get('key1')).toEqual({ data: 'hello' });
    });

    it('should return null for non-existent keys', () => {
      const cache = new MemoryCache();
      expect(cache.get('nonexistent')).toBeNull();
    });

    it('should return null for expired entries', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'value', 1000);

      vi.advanceTimersByTime(1500);

      expect(cache.get('key1')).toBeNull();
    });

    it('should evict oldest entry when at capacity', () => {
      const cache = new MemoryCache(3);
      cache.set('a', 1, 60000);
      cache.set('b', 2, 60000);
      cache.set('c', 3, 60000);

      // Adding fourth should evict first
      cache.set('d', 4, 60000);

      expect(cache.get('a')).toBeNull();
      expect(cache.get('b')).toBe(2);
      expect(cache.get('c')).toBe(3);
      expect(cache.get('d')).toBe(4);
    });

    it('should update LRU order on access', () => {
      const cache = new MemoryCache(3);
      cache.set('a', 1, 60000);
      cache.set('b', 2, 60000);
      cache.set('c', 3, 60000);

      // Access 'a' to move it to end
      cache.get('a');

      // Adding new entry should evict 'b' (now oldest)
      cache.set('d', 4, 60000);

      expect(cache.get('a')).toBe(1);
      expect(cache.get('b')).toBeNull();
    });

    it('should store entries with tags', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'value', 60000, ['tag1', 'tag2']);

      expect(cache.get('key1')).toBe('value');
    });
  });

  describe('has', () => {
    it('should return true for existing non-expired entry', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'value', 60000);

      expect(cache.has('key1')).toBe(true);
    });

    it('should return false for non-existent entry', () => {
      const cache = new MemoryCache();
      expect(cache.has('nonexistent')).toBe(false);
    });

    it('should return false for expired entry', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'value', 100);

      vi.advanceTimersByTime(200);

      expect(cache.has('key1')).toBe(false);
    });
  });

  describe('invalidate', () => {
    it('should remove specific key', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'value1', 60000);
      cache.set('key2', 'value2', 60000);

      const result = cache.invalidate('key1');

      expect(result).toBe(true);
      expect(cache.get('key1')).toBeNull();
      expect(cache.get('key2')).toBe('value2');
    });

    it('should return false for non-existent key', () => {
      const cache = new MemoryCache();
      expect(cache.invalidate('nonexistent')).toBe(false);
    });
  });

  describe('invalidateByTag', () => {
    it('should remove entries with matching tag', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'v1', 60000, ['users']);
      cache.set('key2', 'v2', 60000, ['posts']);
      cache.set('key3', 'v3', 60000, ['users', 'cached']);

      const count = cache.invalidateByTag('users');

      expect(count).toBe(2);
      expect(cache.get('key1')).toBeNull();
      expect(cache.get('key2')).toBe('v2');
      expect(cache.get('key3')).toBeNull();
    });

    it('should return 0 when no entries match', () => {
      const cache = new MemoryCache();
      cache.set('key1', 'v1', 60000, ['users']);

      expect(cache.invalidateByTag('posts')).toBe(0);
    });
  });

  describe('invalidateByPattern', () => {
    it('should remove entries matching regex', () => {
      const cache = new MemoryCache();
      cache.set('/api/users/1', 'u1', 60000);
      cache.set('/api/users/2', 'u2', 60000);
      cache.set('/api/posts/1', 'p1', 60000);

      const count = cache.invalidateByPattern(/\/api\/users/);

      expect(count).toBe(2);
      expect(cache.get('/api/users/1')).toBeNull();
      expect(cache.get('/api/posts/1')).toBe('p1');
    });
  });

  describe('invalidateByPath/invalidatePath', () => {
    it('should remove entries with path prefix', () => {
      const cache = new MemoryCache();
      cache.set('/api/users/1', 'u1', 60000);
      cache.set('/api/users/2', 'u2', 60000);
      cache.set('/api/posts/1', 'p1', 60000);

      const count = cache.invalidateByPath('/api/users');

      expect(count).toBe(2);
      expect(cache.get('/api/posts/1')).toBe('p1');
    });

    it('invalidatePath should be alias for invalidateByPath', () => {
      const cache = new MemoryCache();
      cache.set('/api/users/1', 'u1', 60000);

      const count = cache.invalidatePath('/api/users');

      expect(count).toBe(1);
      expect(cache.get('/api/users/1')).toBeNull();
    });
  });

  describe('invalidateTags', () => {
    it('should invalidate multiple tags', () => {
      const cache = new MemoryCache();
      cache.set('k1', 'v1', 60000, ['users']);
      cache.set('k2', 'v2', 60000, ['posts']);
      cache.set('k3', 'v3', 60000, ['comments']);

      const count = cache.invalidateTags(['users', 'posts']);

      expect(count).toBe(2);
      expect(cache.get('k3')).toBe('v3');
    });
  });

  describe('clear', () => {
    it('should remove all entries', () => {
      const cache = new MemoryCache();
      cache.set('k1', 'v1', 60000);
      cache.set('k2', 'v2', 60000);

      cache.clear();

      expect(cache.get('k1')).toBeNull();
      expect(cache.get('k2')).toBeNull();
      expect(cache.stats().size).toBe(0);
    });
  });

  describe('stats', () => {
    it('should return current size and max entries', () => {
      const cache = new MemoryCache(50);
      cache.set('k1', 'v1', 60000);
      cache.set('k2', 'v2', 60000);

      const stats = cache.stats();

      expect(stats.size).toBe(2);
      expect(stats.maxEntries).toBe(50);
    });
  });
});

describe('createCacheInterceptor', () => {
  it('should create interceptor with cache', () => {
    const interceptor = createCacheInterceptor();

    expect(interceptor.cache).toBeDefined();
    expect(interceptor.cache).toBeInstanceOf(MemoryCache);
    expect(interceptor.onRequest).toBeDefined();
    expect(interceptor.onResponse).toBeDefined();
  });

  it('should use custom max entries', () => {
    const interceptor = createCacheInterceptor({ maxEntries: 50 });

    expect(interceptor.cache.stats().maxEntries).toBe(50);
  });

  it('should pass through non-GET requests on onRequest', async () => {
    const interceptor = createCacheInterceptor();

    const request = { method: 'POST', path: '/api/users', body: { name: 'Test' } } satisfies RequestConfig;
    const result = await interceptor.onRequest!(request);

    expect(result).toEqual(request);
  });

  it('should pass through GET requests when not cached', async () => {
    const interceptor = createCacheInterceptor();

    const request = { method: 'GET', path: '/api/users' } satisfies RequestConfig;
    const result = await interceptor.onRequest!(request);

    expect(result.method).toBe('GET');
    expect(result.path).toBe('/api/users');
  });

  it('should use custom cacheMethods', async () => {
    const interceptor = createCacheInterceptor({
      cacheMethods: ['GET', 'HEAD'],
    });

    // HEAD should not be ignored like POST would be
    const request = { method: 'HEAD', path: '/api/check' } satisfies RequestConfig;
    const result = await interceptor.onRequest!(request);

    expect(result).toBeDefined();
  });
});
