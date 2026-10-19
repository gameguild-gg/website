/**
 * Tests for React Query Integration hooks — extended coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

// Track mock instances for assertions
const cancelQueriesMock = vi.fn(async () => {});
const getQueryDataMock = vi.fn((): unknown => undefined);
const setQueryDataMock = vi.fn();
const invalidateQueriesMock = vi.fn(async () => {});
const refetchQueriesMock = vi.fn(async () => {});

vi.mock('@tanstack/react-query', () => ({
  useQuery: vi.fn((opts: any) => ({
    data: undefined,
    isLoading: true,
    error: null,
    ...opts,
  })),
  useMutation: vi.fn((opts: any) => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isLoading: false,
    ...opts,
  })),
  useQueryClient: vi.fn(() => ({
    cancelQueries: cancelQueriesMock,
    getQueryData: getQueryDataMock,
    setQueryData: setQueryDataMock,
    invalidateQueries: invalidateQueriesMock,
    refetchQueries: refetchQueriesMock,
  })),
}));

import { generateQueryKey, createApiClientHooks, createQueryHook, createMutationHook } from '../../src/integrations/react/query-hooks.js';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '../../src/runtime/errors/types.js';

describe('generateQueryKey', () => {
  it('should generate a key with module and operation', () => {
    const key = generateQueryKey('users', 'list');
    expect(key).toEqual(['users', 'list']);
  });

  it('should include params when provided', () => {
    const key = generateQueryKey('users', 'get', { id: '123' });
    expect(key).toEqual(['users', 'get', { id: '123' }]);
  });

  it('should not include params when empty', () => {
    const key = generateQueryKey('users', 'list', {});
    expect(key).toEqual(['users', 'list']);
  });

  it('should include params with multiple keys', () => {
    const key = generateQueryKey('projects', 'search', { q: 'test', page: 1 });
    expect(key).toEqual(['projects', 'search', { q: 'test', page: 1 }]);
  });
});

describe('createApiClientHooks', () => {
  it('should return a hooks object with client', () => {
    const mockClient = { request: vi.fn() } as any;
    const hooks = createApiClientHooks(mockClient);

    expect(hooks.client).toBe(mockClient);
    expect(hooks.queryClient).toBe(useQueryClient);
  });
});

describe('createQueryHook', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should create a hook factory', () => {
    const queryKeyFactory = (...params: string[]) => ['test', ...params];
    const queryFn = vi.fn(async () => ({ ok: true as const, data: 'result' }));

    const useApiQuery = createQueryHook(queryKeyFactory, queryFn);
    expect(typeof useApiQuery).toBe('function');
  });

  it('should call useQuery with correct queryKey', () => {
    const queryKeyFactory = (id: string) => ['users', id];
    const queryFn = vi.fn(async (id: string) => ({ ok: true as const, data: { id } }));

    const useApiQuery = createQueryHook(queryKeyFactory, queryFn);
    useApiQuery(['user-123'] as any);

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: ['users', 'user-123'],
      }),
    );
  });

  it('should unwrap successful results in queryFn', async () => {
    const queryFn = vi.fn(async () => ({ ok: true as const, data: { name: 'Test' } }));
    const useApiQuery = createQueryHook(() => ['items'], queryFn);
    useApiQuery([] as any);

    const opts = (useQuery as any).mock.calls[0][0];
    const result = await opts.queryFn();
    expect(result).toEqual({ name: 'Test' });
  });

  it('should throw error for failed results in queryFn', async () => {
    const apiError: ApiError = {
      name: 'ApiError',
      message: 'Not found',
      status: 404,
      code: 'NOT_FOUND',
    };
    const queryFn = vi.fn(async () => ({ ok: false as const, error: apiError }));
    const useApiQuery = createQueryHook(() => ['items'], queryFn);
    useApiQuery([] as any);

    const opts = (useQuery as any).mock.calls[0][0];
    await expect(opts.queryFn()).rejects.toEqual(apiError);
  });

  it('should pass through additional query options', () => {
    const queryFn = vi.fn(async () => ({ ok: true as const, data: null }));
    const useApiQuery = createQueryHook(() => ['items'], queryFn);
    useApiQuery([] as any, { staleTime: 5000, enabled: false });

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        staleTime: 5000,
        enabled: false,
      }),
    );
  });
});

describe('createMutationHook', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getQueryDataMock.mockReturnValue(undefined);
  });

  it('should create a mutation hook factory', () => {
    const mutationFn = vi.fn(async (vars: { name: string }) => ({
      ok: true as const,
      data: { id: '1', name: vars.name },
    }));

    const useApiMutation = createMutationHook(mutationFn);
    expect(typeof useApiMutation).toBe('function');
  });

  it('should call useMutation', () => {
    const mutationFn = vi.fn(async (vars: any) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    expect(useMutation).toHaveBeenCalled();
  });

  it('should unwrap error results by throwing', async () => {
    const apiError: ApiError = {
      name: 'ApiError',
      message: 'Bad request',
      status: 400,
      code: 'VALIDATION_ERROR',
    };
    const mutationFn = vi.fn(async () => ({ ok: false as const, error: apiError }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    const opts = (useMutation as any).mock.calls[0][0];
    await expect(opts.mutationFn({})).rejects.toEqual(apiError);
  });

  it('should unwrap successful results in mutationFn', async () => {
    const mutationFn = vi.fn(async (vars: any) => ({
      ok: true as const,
      data: { id: '1', ...vars },
    }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    const opts = (useMutation as any).mock.calls[0][0];
    const result = await opts.mutationFn({ name: 'Test' });
    expect(result).toEqual({ id: '1', name: 'Test' });
  });

  it('should handle optimistic onMutate: cancel queries, snapshot, set data', async () => {
    getQueryDataMock.mockReturnValue({ items: ['old'] });

    const mutationFn = vi.fn(async (vars: any) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      optimistic: {
        optimisticData: (vars: any, current: any) => ({
          items: [...(current?.items || []), vars.name],
        }),
        invalidateKeys: [['items']],
        rollbackOnError: true,
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    const ctx = await opts.onMutate({ name: 'new' }, undefined);

    // Should cancel queries
    expect(cancelQueriesMock).toHaveBeenCalledWith({ queryKey: ['items'] });
    // Should set optimistic data
    expect(setQueryDataMock).toHaveBeenCalledWith(['items'], { items: ['old', 'new'] });
    // Should return previousData for rollback
    expect(ctx.previousData).toBeDefined();
  });

  it('should call user onMutate alongside optimistic onMutate', async () => {
    const customOnMutate = vi.fn();
    const mutationFn = vi.fn(async (vars: any) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      onMutate: customOnMutate,
      optimistic: {
        optimisticData: (vars: any) => vars,
        invalidateKeys: [['items']],
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    await opts.onMutate({ name: 'test' }, undefined);
    expect(customOnMutate).toHaveBeenCalled();
  });

  it('should call user onMutate without optimistic config', async () => {
    const customOnMutate = vi.fn();
    const mutationFn = vi.fn(async (vars: any) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({ onMutate: customOnMutate });

    const opts = (useMutation as any).mock.calls[0][0];
    await opts.onMutate({ name: 'test' }, undefined);
    expect(customOnMutate).toHaveBeenCalledWith({ name: 'test' }, undefined);
  });

  it('should rollback on error when rollbackOnError is true', async () => {
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      optimistic: {
        optimisticData: (vars: any) => vars,
        invalidateKeys: [['items']],
        rollbackOnError: true,
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    const error: ApiError = { name: 'ApiError', message: 'fail', status: 500, code: 'SERVER_ERROR' };
    const onMutateResult = {
      previousData: [{ key: ['items'], data: { old: true } }],
    };

    await opts.onError(error, {}, onMutateResult, undefined);
    expect(setQueryDataMock).toHaveBeenCalledWith(['items'], { old: true });
  });

  it('should call user onError handler', async () => {
    const customOnError = vi.fn();
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      onError: customOnError,
      optimistic: {
        rollbackOnError: false,
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    const error: ApiError = { name: 'ApiError', message: 'fail', status: 500, code: 'SERVER_ERROR' };
    await opts.onError(error, {}, null, undefined);
    expect(customOnError).toHaveBeenCalled();
  });

  it('should invalidate and refetch on success', async () => {
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      optimistic: {
        invalidateKeys: [['items']],
        refetchKeys: [['users']],
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    await opts.onSuccess({}, {}, undefined, undefined);

    expect(invalidateQueriesMock).toHaveBeenCalledWith({ queryKey: ['items'] });
    expect(refetchQueriesMock).toHaveBeenCalledWith({ queryKey: ['users'] });
  });

  it('should call user onSuccess handler', async () => {
    const customOnSuccess = vi.fn();
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      onSuccess: customOnSuccess,
      optimistic: {
        invalidateKeys: [['items']],
      },
    });

    const opts = (useMutation as any).mock.calls[0][0];
    await opts.onSuccess({ id: '1' }, { name: 'test' }, undefined, undefined);
    expect(customOnSuccess).toHaveBeenCalled();
  });

  it('should handle onSuccess without optimistic keys', async () => {
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    const opts = (useMutation as any).mock.calls[0][0];
    // Should not throw
    await opts.onSuccess({}, {}, undefined, undefined);
    expect(invalidateQueriesMock).not.toHaveBeenCalled();
  });
});
