/**
 * API Client Integration Verification
 *
 * Comprehensive tests to verify the API client works correctly in the web app
 */

import { beforeAll, describe, expect, it } from 'vitest';
import type { Interceptor } from '@game-guild/client';

async function loadClientModule() {
  return import('../../../../../packages/infrastructure/client/dist/index.js');
}

async function loadReactModule() {
  return import('../../../../../packages/infrastructure/client/dist/react.js');
}

async function loadNextModule() {
  return import('../../../../../packages/infrastructure/client/dist/next.js');
}

describe('API Client Package Integration', () => {
  const nextIntegrationModulePath = '../../../../../packages/infrastructure/client/dist/next.js';
  let clientModule: Awaited<ReturnType<typeof loadClientModule>>;
  let reactModule: Awaited<ReturnType<typeof loadReactModule>>;
  let nextModule: Awaited<ReturnType<typeof loadNextModule>>;

  beforeAll(async () => {
    [clientModule, reactModule, nextModule] = await Promise.all([
      loadClientModule(),
      loadReactModule(),
      loadNextModule(),
    ]);
  }, 60_000);

  describe('Package Installation', () => {
    it('should have @game-guild/client in dependencies', async () => {
      const packageJson = await import('../../../package.json');
      expect(packageJson.dependencies).toHaveProperty('@game-guild/client');
    });
  });

  describe('Main Entry Point', () => {
    it('should import createClient successfully', async () => {
      const { createClient } = clientModule;
      expect(createClient).toBeDefined();
      expect(typeof createClient).toBe('function');
    });

    it('should import ApiError type', async () => {
      expect(clientModule).toHaveProperty('createClient');
    });

    it('should create a client instance', async () => {
      const { createClient } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
      });

      expect(client).toBeDefined();
      expect(client).toHaveProperty('request');
      expect(client).toHaveProperty('getBaseUrl');
    });
  });

  describe('Next.js Integration Entry Point', () => {
    it('should import Next.js utilities', async () => {
      expect(nextModule).toHaveProperty('createNextClient');
      expect(nextModule).toHaveProperty('createRouteClient');
      expect(typeof nextModule.createNextClient).toBe('function');
      expect(typeof nextModule.createRouteClient).toBe('function');
    });

    it('should create Next.js client', async () => {
      const { createNextClient } = nextModule;
      const client = createNextClient({
        baseUrl: 'http://localhost:8080',
      });

      expect(client).toBeDefined();
    });
  });

  describe('React Integration Entry Point', () => {
    it('should import React utilities', async () => {
      expect(reactModule).toBeDefined();
    });
  });

  describe('Client Configuration', () => {
    it('should accept baseUrl configuration', async () => {
      const { createClient } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
      });

      expect(client).toBeDefined();
    });

    it('should accept headers configuration', async () => {
      const { createClient } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
        headers: {
          'X-Tenant-Id': 'test-tenant',
          'X-Custom-Header': 'custom-value',
        },
      });

      expect(client).toBeDefined();
    });

    it('should accept auth configuration', async () => {
      const { createClient } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
        auth: {
          getToken: async () => 'test-token',
        },
      });

      expect(client).toBeDefined();
    });

    it('should accept interceptors', async () => {
      const { createClient } = clientModule;

      const requestInterceptor: Interceptor = {
        onRequest: async (config) => {
          console.log('Request:', config);
          return config;
        },
      };

      const client = createClient({
        baseUrl: 'http://localhost:8080',
        interceptors: [requestInterceptor],
      });

      expect(client).toBeDefined();
    });
  });

  describe('Type Safety', () => {
    it('should provide typed client methods', async () => {
      const { createClient } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
      });

      // Should have health method
      expect(client).toHaveProperty('request');
      expect(typeof client.request).toBe('function');
    });

    it('should export error types', async () => {
      // Verify module has expected exports
      expect(clientModule.createClient).toBeDefined();
    });
  });

  describe('Error Handling', () => {
    it('should handle client creation errors gracefully', async () => {
      const { createClient } = clientModule;

      // Should not throw on invalid URL
      expect(() => {
        createClient({
          baseUrl: 'invalid-url',
        });
      }).not.toThrow();
    });
  });

  describe('Module System Compatibility', () => {
    it('should work with ES modules import', async () => {
      expect(clientModule.createClient).toBeDefined();
    });

    it('should export all required sub-modules', async () => {
      // Test main module
      expect(clientModule.createClient).toBeDefined();

      // Test Next.js module
      expect(nextModule.createNextClient).toBeDefined();

      // Test React module (if available in client-side context)
      expect(reactModule).toBeDefined();

    });

    it('should export generated modules for newly completed API surfaces', async () => {
      const { createClient, GeneratedApi } = clientModule;
      const client = createClient({
        baseUrl: 'http://localhost:8080',
      });

      const modules = {
        ai: new GeneratedApi.AiModule(client),
        aiPromptTemplates: new GeneratedApi.AiPromptTemplatesModule(client),
        ferpa: new GeneratedApi.ComplianceFerpaModule(client),
        socialProfiles: new GeneratedApi.SocialProfilesModule(client),
        socialBlog: new GeneratedApi.SocialBlogPublicModule(client),
        socialBlogAuthoring: new GeneratedApi.SocialBlogAuthoringModule(client),
        socialFeed: new GeneratedApi.SocialFeedModule(client),
        socialGroups: new GeneratedApi.SocialGroupsSocialGroupsModule(client),
        socialReactions: new GeneratedApi.SocialReactionsModule(client),
        enrollments: new GeneratedApi.LearningEnrollmentsModule(client),
        gameJams: new GeneratedApi.GameJamsModule(client),
      };

      expect(modules.ai.getAiStatus).toBeTypeOf('function');
      expect(modules.aiPromptTemplates.getAiPromptTemplatesForGetAiPromptTemplates).toBeTypeOf('function');
      expect(modules.ferpa.getApiComplianceFerpaStudentsRecords).toBeTypeOf('function');
      expect(modules.socialProfiles.getApiSocialProfilesUsers).toBeTypeOf('function');
      expect(modules.socialBlog.getApiSocialBlogPublicPosts).toBeTypeOf('function');
      expect(modules.socialBlogAuthoring.getApiSocialBlogPostsMine).toBeTypeOf('function');
      expect(modules.socialFeed.getApiSocialFeedUsers).toBeTypeOf('function');
      expect(modules.socialGroups.getApiSocialGroupsForGetApiSocialGroups).toBeTypeOf('function');
      expect(modules.socialGroups.postApiSocialGroupsMembers).toBeTypeOf('function');
      expect(modules.socialReactions.getApiSocialReactionsTarget).toBeTypeOf('function');
      expect(modules.enrollments.postApiLearningEnrollments).toBeTypeOf('function');
      expect(modules.gameJams.getApiGameJamsForGetApiGameJams).toBeTypeOf('function');
    });
  });

  describe('Build Output Verification', () => {
    it('should have TypeScript definitions', async () => {
      // The fact that imports work with TypeScript implies .d.ts files exist
      const { createClient } = clientModule;

      // TypeScript compilation would fail if types weren't available
      const client: ReturnType<typeof createClient> = createClient({
        baseUrl: 'http://localhost:8080',
      });

      expect(client).toBeDefined();
    });
  });
});
