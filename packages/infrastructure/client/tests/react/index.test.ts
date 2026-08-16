/**
 * @vitest-environment happy-dom
 *
 * Tests for React Integration Index
 *
 * Tests the runtime React hook implementations and type exports.
 */

import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement, type ReactNode } from 'react';
import { ApiClientProvider, useApiClient, useQuery, useMutation, useOptimisticUpdate, ApiClientContext } from '../../src/integrations/react/index.js';
import type { ApiClient } from '../../src/runtime/client.js';

function createWrapper(client: ApiClient) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, createElement(ApiClientProvider, { client }, children));
  };
}

const apiClient: ApiClient = {
  request: async () => ({ ok: true, data: { ok: true } }),
  requestRaw: async () => ({
    ok: true,
    data: { data: { ok: true }, status: 200, headers: new Headers() },
  }),
  getBaseUrl: () => 'https://api.example.test',
};

describe('React Integration', () => {
  describe('ApiClientContext', () => {
    it('should have a displayName', () => {
      expect(ApiClientContext.displayName).toBe('ApiClientContext');
    });
  });

  describe('useApiClient', () => {
    it('returns the provider client', () => {
      const { result } = renderHook(() => useApiClient(), {
        wrapper: createWrapper(apiClient),
      });

      expect(result.current.getBaseUrl()).toBe('https://api.example.test');
    });
  });

  describe('useQuery', () => {
    it('loads result data through React Query', async () => {
      const { result } = renderHook(() => useQuery(['key'], async () => ({ ok: true, data: 'loaded' })), { wrapper: createWrapper(apiClient) });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toBe('loaded');
      expect(result.current.error).toBeUndefined();
    });
  });

  describe('useMutation', () => {
    it('executes mutations and exposes result state', async () => {
      const { result } = renderHook(() => useMutation(async (value: string) => ({ ok: true, data: value.toUpperCase() })), {
        wrapper: createWrapper(apiClient),
      });

      const mutation = await result.current.mutate('saved');

      expect(mutation).toEqual({ ok: true, data: 'SAVED' });
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toBe('SAVED');
    });
  });

  describe('useOptimisticUpdate', () => {
    it('updates and rolls back cached data', () => {
      const { result } = renderHook(() => useOptimisticUpdate<{ count: number }>(['counter']), {
        wrapper: createWrapper(apiClient),
      });

      result.current.update((old) => ({ count: (old?.count ?? 0) + 1 }));
      expect(result.current.get()).toEqual({ count: 1 });

      result.current.rollback();
      expect(result.current.get()).toBeUndefined();
    });

    it('throws outside the API client provider', () => {
      expect(() =>
        renderHook(() => useApiClient(), {
          wrapper: ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client: new QueryClient() }, children),
        }),
      ).toThrow('useApiClient must be used within an <ApiClientProvider>.');
    });
  });
});
