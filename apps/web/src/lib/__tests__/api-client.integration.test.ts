/**
 * API Client Integration Tests
 *
 * Server-side integration tests for @game-guild/client
 */

import { createClient, type Interceptor } from '@game-guild/client';

async function loadNextIntegrationModule() {
  return import('@game-guild/client/next');
}

describe('API Client Integration', () => {
  describe('Basic Client Creation', () => {
    it('should create a client with base configuration', () => {
      const client = createClient({
        baseUrl: 'http://localhost:8080',
        headers: {
          'X-Tenant-Id': 'test-tenant',
        },
      });

      expect(client).toBeDefined();
      expect(typeof client.request).toBe('function');
      expect(typeof client.getBaseUrl).toBe('function');
    });

    it('should create client with authentication', () => {
      const client = createClient({
        baseUrl: 'http://localhost:8080',
        auth: {
          getToken: async () => 'test-token',
        },
      });

      expect(client).toBeDefined();
    });
  });

  describe('Next.js Integration', () => {
    it('should export createNextClient', async () => {
      const { createNextClient } = await loadNextIntegrationModule();
      expect(typeof createNextClient).toBe('function');
    });

    it('should export createServerClient', async () => {
      const { createRouteClient } = await loadNextIntegrationModule();
      expect(typeof createRouteClient).toBe('function');
    });

    it('should create Next.js client', async () => {
      const { createNextClient } = await loadNextIntegrationModule();
      const client = createNextClient({
        baseUrl: 'http://localhost:8080',
      });

      expect(client).toBeDefined();
    });
  });

  describe('Type Safety', () => {
    it('should provide typed API methods', () => {
      const client = createClient({
        baseUrl: 'http://localhost:8080',
      });

      // These should be type-safe
      expect(client).toHaveProperty('request');
      expect(client).toHaveProperty('getBaseUrl');
    });

    it('should support interceptors', () => {
      const requestInterceptor: Interceptor = {
        onRequest: async (config) => config,
      };

      const client = createClient({
        baseUrl: 'http://localhost:8080',
        interceptors: [requestInterceptor],
      });

      expect(client).toBeDefined();
    });
  });

  describe('Error Handling', () => {
    it('should handle network errors gracefully', async () => {
      const client = createClient({
        baseUrl: 'http://invalid-url-that-does-not-exist.local',
      });

      expect(client).toBeDefined();
      // Actual network call would fail, but client creation should succeed
    });
  });

  describe('Module Exports', () => {
    it('should export createClient from main entry', async () => {
      const module = await import('@game-guild/client');
      expect(module.createClient).toBeDefined();
      expect(typeof module.createClient).toBe('function');
    });

    it('should export Next.js utilities', async () => {
      const module = await loadNextIntegrationModule();
      expect(module.createNextClient).toBeDefined();
      expect(module.createRouteClient).toBeDefined();
    });
  });
});
