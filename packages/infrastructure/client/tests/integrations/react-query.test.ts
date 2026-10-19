// @vitest-environment happy-dom

/**
 * React Query Integration Tests
 * 
 * Tests for React Query hooks with optimistic updates
 */

import { afterEach, describe, it, expect } from 'vitest';
import { createElement, type PropsWithChildren } from 'react';
import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createQueryHook,
  createMutationHook,
  generateQueryKey,
  type OptimisticUpdateConfig,
} from '../../src/integrations/react/query-hooks.js';
import { ok, err } from '../../src/runtime/result/helpers.js';
import type { Result } from '../../src/runtime/result/types.js';
import type { ApiError } from '../../src/runtime/errors/types.js';

const clients: QueryClient[] = [];

function createQueryClient() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { retry: false, gcTime: Infinity },
    },
  });
  clients.push(client);
  return client;
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: PropsWithChildren) {
    return createElement(QueryClientProvider, { client, children });
  };
}

afterEach(() => {
  cleanup();
  for (const client of clients) client.clear();
  clients.length = 0;
});

describe('React Query Integration', () => {
  describe('Query Hook Factory', () => {
    it('should create query hook with correct signature', () => {
      const queryKeyFactory = (userId: string) => ['users', userId];
      const queryFn = async (userId: string): Promise<Result<{ id: string; name: string }, ApiError>> => {
        return ok({ id: userId, name: 'Test User' });
      };

      const useUserQuery = createQueryHook(queryKeyFactory, queryFn);

      expect(useUserQuery).toBeDefined();
      expect(typeof useUserQuery).toBe('function');
    });
  });

  describe('Mutation Hook Factory', () => {
    it('should create mutation hook with correct signature', () => {
      const mutationFn = async (data: { name: string }): Promise<Result<{ id: string; name: string }, ApiError>> => {
        return ok({ id: '123', name: data.name });
      };

      const useCreateUser = createMutationHook(mutationFn);

      expect(useCreateUser).toBeDefined();
      expect(typeof useCreateUser).toBe('function');
    });

    it('should support optimistic update configuration', async () => {
      const mutationFn = async (data: { name: string }): Promise<Result<{ id: string; name: string }, ApiError>> => {
        return ok({ id: '123', name: data.name });
      };

      const optimisticConfig: OptimisticUpdateConfig<{ id: string; name: string }, { name: string }> = {
        optimisticData: (variables) => ({
          id: 'temp-id',
          name: variables.name,
        }),
        rollbackOnError: true,
        invalidateKeys: [['users']],
      };

      const useCreateUser = createMutationHook(mutationFn);

      // Should accept optimistic config
      expect(useCreateUser).toBeDefined();

      const client = createQueryClient();
      client.setQueryData(['users'], { id: 'existing-id', name: 'Previous' });
      const { result } = renderHook(() => useCreateUser({ optimistic: optimisticConfig }), {
        wrapper: wrapperFor(client),
      });

      await act(async () => {
        await expect(result.current.mutateAsync({ name: 'New User' })).resolves.toEqual({
          id: '123', name: 'New User',
        });
      });
      expect(client.getQueryData(['users'])).toEqual({ id: 'temp-id', name: 'New User' });
    });
  });

  describe('Query Key Generation', () => {
    it('should generate query keys without params', () => {
      const key = generateQueryKey('users', 'list');
      expect(key).toEqual(['users', 'list']);
    });

    it('should generate query keys with params', () => {
      const key = generateQueryKey('users', 'getById', { id: '123' });
      expect(key).toEqual(['users', 'getById', { id: '123' }]);
    });

    it('should ignore empty params object', () => {
      const key = generateQueryKey('users', 'list', {});
      expect(key).toEqual(['users', 'list']);
    });

    it('should generate consistent keys for same inputs', () => {
      const key1 = generateQueryKey('posts', 'getById', { id: '456', include: 'comments' });
      const key2 = generateQueryKey('posts', 'getById', { id: '456', include: 'comments' });
      
      expect(key1).toEqual(key2);
    });
  });

  describe('Optimistic Update Config', () => {
    it('should define optimistic data generator', () => {
      const config: OptimisticUpdateConfig<{ count: number }, { increment: number }> = {
        optimisticData: (variables, currentData) => ({
          count: (currentData?.count || 0) + variables.increment,
        }),
      };

      expect(config.optimisticData).toBeDefined();
      
      const optimisticResult = config.optimisticData!({ increment: 5 }, { count: 10 });
      expect(optimisticResult.count).toBe(15);
    });

    it('should handle undefined current data', () => {
      const config: OptimisticUpdateConfig<{ items: string[] }, { item: string }> = {
        optimisticData: (variables, currentData) => ({
          items: [...(currentData?.items || []), variables.item],
        }),
      };

      const optimisticResult = config.optimisticData!({ item: 'new-item' }, undefined);
      expect(optimisticResult.items).toEqual(['new-item']);
    });

    it('should support invalidate and refetch keys', () => {
      const config: OptimisticUpdateConfig<unknown, unknown> = {
        invalidateKeys: [['users'], ['posts']],
        refetchKeys: [['stats']],
        rollbackOnError: true,
      };

      expect(config.invalidateKeys).toHaveLength(2);
      expect(config.refetchKeys).toHaveLength(1);
      expect(config.rollbackOnError).toBe(true);
    });
  });

  describe('Error Handling', () => {
    it('should handle mutation function errors', async () => {
      const error: ApiError = {
        name: 'ApiError',
        code: 'VALIDATION_ERROR',
        message: 'Invalid data',
        status: 400,
      };

      const mutationFn = async (): Promise<Result<never, ApiError>> => {
        return err(error);
      };

      const useCreateUser = createMutationHook<never, void>(mutationFn);

      expect(useCreateUser).toBeDefined();
      const client = createQueryClient();
      const { result } = renderHook(() => useCreateUser(), { wrapper: wrapperFor(client) });
      let rejected: unknown;
      await act(async () => {
        try {
          await result.current.mutateAsync(undefined);
        } catch (cause) {
          rejected = cause;
        }
      });
      expect(rejected).toBe(error);
      await waitFor(() => expect(result.current.error).toBe(error));
    });
  });
});
