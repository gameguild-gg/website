// @vitest-environment happy-dom

import { createElement, type PropsWithChildren } from 'react';
import { act, cleanup, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createMutationHook } from '../../src/integrations/react/query-hooks.js';
import { err, ok } from '../../src/runtime/result/helpers.js';
import type { ApiError } from '../../src/runtime/errors/types.js';

interface ItemList {
  items: string[];
}

interface AddItemVariables {
  name: string;
}

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
  vi.restoreAllMocks();
});

describe('mutation lifecycle with the real QueryClient', () => {
  it('updates optimistically and rolls back before custom mutation and error callbacks complete', async () => {
    const client = createQueryClient();
    const initial: ItemList = { items: ['old'] };
    client.setQueryData(['items'], initial);
    const previous = client.getQueryData<ItemList>(['items']);
    const cancelQueries = vi.spyOn(client, 'cancelQueries');
    const events: string[] = [];
    const apiError: ApiError = { name: 'ApiError', code: 'SERVER_ERROR', status: 500, message: 'request failed' };
    const mutationFn = vi.fn(async () => err(apiError));
    const useAddItem = createMutationHook<ItemList, AddItemVariables>(mutationFn);
    const variables = { name: 'new' };
    const { result } = renderHook(() => useAddItem({
      optimistic: {
        invalidateKeys: [['items']],
        optimisticData: (input, current) => ({ items: [...(current?.items ?? []), input.name] }),
        rollbackOnError: true,
      },
      onMutate(input, context) {
        expect(input).toBe(variables);
        expect(context.client).toBe(client);
        expect(client.getQueryData(['items'])).toEqual({ items: ['old', 'new'] });
        events.push('mutate');
        return undefined;
      },
      onError(error, input, snapshot, context) {
        expect(error).toBe(apiError);
        expect(input).toBe(variables);
        expect(context.client).toBe(client);
        expect(snapshot?.previousData[0]?.data).toBe(previous);
        expect(client.getQueryData(['items'])).toEqual(initial);
        events.push('error');
      },
      onSettled() {
        events.push('settled');
      },
    }), { wrapper: wrapperFor(client) });

    await act(async () => {
      await expect(result.current.mutateAsync(variables)).rejects.toBe(apiError);
    });

    expect(cancelQueries).toHaveBeenCalledTimes(1);
    expect(cancelQueries).toHaveBeenCalledWith({ queryKey: ['items'] });
    expect(mutationFn).toHaveBeenCalledTimes(1);
    expect(client.getQueryData(['items'])).toEqual(initial);
    expect(events).toEqual(['mutate', 'error', 'settled']);
  });

  it('invalidates and refetches before awaiting custom success and settled callbacks', async () => {
    const client = createQueryClient();
    client.setQueryData(['items'], { items: ['old'] });
    let fetches = 0;
    await client.prefetchQuery({ queryKey: ['users'], queryFn: async () => ({ version: ++fetches }) });
    const events: string[] = [];
    const useAddItem = createMutationHook<ItemList, AddItemVariables>(async (input) => ok({ items: [input.name] }));
    const variables = { name: 'new' };
    const { result } = renderHook(() => useAddItem({
      optimistic: { invalidateKeys: [['items']], refetchKeys: [['users']] },
      async onSuccess(data, input, snapshot, context) {
        expect(data).toEqual({ items: ['new'] });
        expect(input).toBe(variables);
        expect(snapshot).toBeUndefined();
        expect(context.client).toBe(client);
        expect(client.getQueryState(['items'])?.isInvalidated).toBe(true);
        expect(client.getQueryData(['users'])).toEqual({ version: 2 });
        events.push('success');
        await Promise.resolve();
        events.push('success completed');
      },
      onSettled() {
        events.push('settled');
      },
    }), { wrapper: wrapperFor(client) });

    await act(async () => {
      await expect(result.current.mutateAsync(variables)).resolves.toEqual({ items: ['new'] });
    });

    expect(fetches).toBe(2);
    expect(events).toEqual(['success', 'success completed', 'settled']);
  });

  it('retains optimistic data when rollback is disabled and a custom error callback is present', async () => {
    const client = createQueryClient();
    client.setQueryData(['items'], { items: ['old'] });
    const apiError: ApiError = { name: 'ApiError', code: 'NETWORK_ERROR', status: 0, message: 'disconnected' };
    const events: string[] = [];
    const useAddItem = createMutationHook<ItemList, AddItemVariables>(async () => err(apiError));
    const { result } = renderHook(() => useAddItem({
      optimistic: {
        invalidateKeys: [['items']],
        optimisticData: (input, current) => ({ items: [...(current?.items ?? []), input.name] }),
        rollbackOnError: false,
      },
      onError(error) {
        expect(error).toBe(apiError);
        expect(client.getQueryData(['items'])).toEqual({ items: ['old', 'new'] });
        events.push('error');
      },
    }), { wrapper: wrapperFor(client) });

    await act(async () => {
      await expect(result.current.mutateAsync({ name: 'new' })).rejects.toBe(apiError);
    });

    expect(client.getQueryData(['items'])).toEqual({ items: ['old', 'new'] });
    expect(events).toEqual(['error']);
  });

  it('passes custom mutation context through without optimistic configuration', async () => {
    const client = createQueryClient();
    const snapshot = { previousData: [{ key: ['custom'], data: { value: 'original' } }] };
    const events: string[] = [];
    const useAddItem = createMutationHook<ItemList, AddItemVariables>(async (input) => ok({ items: [input.name] }));
    const { result } = renderHook(() => useAddItem({
      onMutate() {
        events.push('mutate');
        return snapshot;
      },
      onSuccess(data, input, actualSnapshot, context) {
        expect(data).toEqual({ items: ['new'] });
        expect(input).toEqual({ name: 'new' });
        expect(actualSnapshot).toBe(snapshot);
        expect(context.client).toBe(client);
        events.push('success');
      },
      onSettled() {
        events.push('settled');
      },
    }), { wrapper: wrapperFor(client) });

    await act(async () => {
      await expect(result.current.mutateAsync({ name: 'new' })).resolves.toEqual({ items: ['new'] });
    });

    expect(events).toEqual(['mutate', 'success', 'settled']);
  });
});
