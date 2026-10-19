/**
 * Tests for React Query Integration hooks — extended coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { QueryKey } from '@tanstack/react-query';
import type { ApiClient } from '../../src/runtime/client.js';
import type { ApiError } from '../../src/runtime/errors/types.js';

interface QueryOptionsFixture {
  queryKey: QueryKey;
  queryFn: () => Promise<unknown>;
}

interface OptimisticSnapshotFixture {
  previousData: Array<{ key: QueryKey; data: unknown }>;
}

// This unit seam deliberately invokes wrappers with undefined SDK context and a
// null error snapshot. The lifecycle suite separately exercises the real SDK.
interface MutationOptionsFixture {
  mutationFn: (variables: unknown) => Promise<unknown>;
  onMutate: (variables: unknown, context: undefined) => Promise<OptimisticSnapshotFixture | undefined>;
  onError: (error: ApiError, variables: unknown, snapshot: OptimisticSnapshotFixture | null | undefined, context: undefined) => void;
  onSuccess: (data: unknown, variables: unknown, snapshot: OptimisticSnapshotFixture | undefined, context: undefined) => Promise<void>;
}

interface NameVariables {
  name: string;
}

interface ItemList {
  items: string[];
}

// Track exactly the options passed across the mocked React Query boundary.
const { useQueryMock, useMutationMock, cancelQueriesMock, getQueryDataMock, setQueryDataMock, invalidateQueriesMock, refetchQueriesMock } = vi.hoisted(() => ({
  useQueryMock: vi.fn((opts: QueryOptionsFixture) => ({
    data: undefined,
    isLoading: true,
    error: null,
    ...opts,
  })),
  useMutationMock: vi.fn((opts: MutationOptionsFixture) => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isLoading: false,
    ...opts,
  })),
  cancelQueriesMock: vi.fn(async () => {}),
  getQueryDataMock: vi.fn((): unknown => undefined),
  setQueryDataMock: vi.fn(),
  invalidateQueriesMock: vi.fn(async () => {}),
  refetchQueriesMock: vi.fn(async () => {}),
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: useQueryMock,
  useMutation: useMutationMock,
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

function queryOptions() {
  const options = useQueryMock.mock.calls[0]?.[0];
  if (!options) throw new Error('Expected captured query options');
  return options;
}

function mutationOptions() {
  const options = useMutationMock.mock.calls[0]?.[0];
  if (!options) throw new Error('Expected captured mutation options');
  return options;
}

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
    const mockClient: ApiClient = {
      request: vi.fn(async () => { throw new Error('Unexpected request in client identity fixture'); }),
      requestRaw: vi.fn(async () => { throw new Error('Unexpected raw request in client identity fixture'); }),
      getBaseUrl: vi.fn(() => 'https://api.example.test'),
    };
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
    useApiQuery(['user-123']);

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: ['users', 'user-123'],
      }),
    );
  });

  it('should unwrap successful results in queryFn', async () => {
    const queryFn = vi.fn(async () => ({ ok: true as const, data: { name: 'Test' } }));
    const useApiQuery = createQueryHook(() => ['items'], queryFn);
    useApiQuery([]);

    const opts = queryOptions();
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
    useApiQuery([]);

    const opts = queryOptions();
    await expect(opts.queryFn()).rejects.toEqual(apiError);
  });

  it('should pass through additional query options', () => {
    const queryFn = vi.fn(async () => ({ ok: true as const, data: null }));
    const useApiQuery = createQueryHook(() => ['items'], queryFn);
    useApiQuery([], { staleTime: 5000, enabled: false });

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
    const mutationFn = vi.fn(async (vars: unknown) => ({ ok: true as const, data: vars }));
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

    const opts = mutationOptions();
    await expect(opts.mutationFn({})).rejects.toEqual(apiError);
  });

  it('should unwrap successful results in mutationFn', async () => {
    const mutationFn = vi.fn(async (vars: NameVariables) => ({
      ok: true as const,
      data: { id: '1', ...vars },
    }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    const opts = mutationOptions();
    const result = await opts.mutationFn({ name: 'Test' });
    expect(result).toEqual({ id: '1', name: 'Test' });
  });

  it('should handle optimistic onMutate: cancel queries, snapshot, set data', async () => {
    getQueryDataMock.mockReturnValue({ items: ['old'] });

    const mutationFn = vi.fn(async (vars: NameVariables) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook<NameVariables, NameVariables, ItemList>(mutationFn);
    useApiMutation({
      optimistic: {
        optimisticData: (vars, current) => ({
          items: [...(current?.items || []), vars.name],
        }),
        invalidateKeys: [['items']],
        rollbackOnError: true,
      },
    });

    const opts = mutationOptions();
    const ctx = await opts.onMutate({ name: 'new' }, undefined);

    // Should cancel queries
    expect(cancelQueriesMock).toHaveBeenCalledWith({ queryKey: ['items'] });
    // Should set optimistic data
    expect(setQueryDataMock).toHaveBeenCalledWith(['items'], { items: ['old', 'new'] });
    // Should return previousData for rollback
    expect(ctx?.previousData).toBeDefined();
  });

  it('should call user onMutate alongside optimistic onMutate', async () => {
    const customOnMutate = vi.fn();
    const mutationFn = vi.fn(async (vars: NameVariables) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({
      onMutate: customOnMutate,
      optimistic: {
        optimisticData: (vars) => vars,
        invalidateKeys: [['items']],
      },
    });

    const opts = mutationOptions();
    await opts.onMutate({ name: 'test' }, undefined);
    expect(customOnMutate).toHaveBeenCalled();
  });

  it('should call user onMutate without optimistic config', async () => {
    const customOnMutate = vi.fn();
    const mutationFn = vi.fn(async (vars: NameVariables) => ({ ok: true as const, data: vars }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation({ onMutate: customOnMutate });

    const opts = mutationOptions();
    await opts.onMutate({ name: 'test' }, undefined);
    expect(customOnMutate).toHaveBeenCalledWith({ name: 'test' }, undefined);
  });

  it('should rollback on error when rollbackOnError is true', async () => {
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook<Record<string, never>, Record<string, never>>(mutationFn);
    useApiMutation({
      optimistic: {
        optimisticData: (vars) => vars,
        invalidateKeys: [['items']],
        rollbackOnError: true,
      },
    });

    const opts = mutationOptions();
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

    const opts = mutationOptions();
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

    const opts = mutationOptions();
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

    const opts = mutationOptions();
    await opts.onSuccess({ id: '1' }, { name: 'test' }, undefined, undefined);
    expect(customOnSuccess).toHaveBeenCalled();
  });

  it('should handle onSuccess without optimistic keys', async () => {
    const mutationFn = vi.fn(async () => ({ ok: true as const, data: {} }));
    const useApiMutation = createMutationHook(mutationFn);
    useApiMutation();

    const opts = mutationOptions();
    // Should not throw
    await opts.onSuccess({}, {}, undefined, undefined);
    expect(invalidateQueriesMock).not.toHaveBeenCalled();
  });
});
