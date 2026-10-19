/**
 * React Query Integration
 *
 * React Query hooks for API client with optimistic updates support
 */

import { useQuery, useMutation, useQueryClient, type UseQueryOptions, type UseMutationOptions, type QueryKey } from '@tanstack/react-query';
import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';

/**
 * Optimistic update configuration
 */
export interface OptimisticUpdateConfig<TData, TVariables> {
  /**
   * Optimistic data to show immediately
   */
  optimisticData?: (variables: TVariables, currentData?: TData) => TData;

  /**
   * Rollback on error
   */
  rollbackOnError?: boolean;

  /**
   * Query keys to invalidate on success
   */
  invalidateKeys?: QueryKey[];

  /**
   * Query keys to refetch on success
   */
  refetchKeys?: QueryKey[];
}

interface OptimisticMutationContext {
  previousData: Array<{ key: QueryKey; data: unknown }>;
}

/**
 * Extended mutation options with optimistic updates
 */
export interface ExtendedMutationOptions<TData, TVariables, TCache = TData> extends Omit<
  UseMutationOptions<TData, ApiError, TVariables, OptimisticMutationContext | undefined>,
  'mutationFn'
> {
  /**
   * Optimistic cache data can differ from the mutation response. By default it
   * retains the response type used by existing callers.
   */
  optimistic?: OptimisticUpdateConfig<TCache, TVariables>;
}

/**
 * Transform Result<T, ApiError> to throw on error for React Query
 */
function unwrapResult<T>(result: Result<T, ApiError>): T {
  if (result.ok) {
    return result.data;
  }
  throw result.error;
}

/**
 * Create query hook factory
 */
export function createQueryHook<TData, TParams extends unknown[]>(
  queryKeyFactory: (...params: TParams) => QueryKey,
  queryFn: (...params: TParams) => Promise<Result<TData, ApiError>>,
) {
  return function useApiQuery(params: TParams, options?: Omit<UseQueryOptions<TData, ApiError>, 'queryKey' | 'queryFn'>) {
    return useQuery<TData, ApiError>({
      queryKey: queryKeyFactory(...params),
      queryFn: async () => unwrapResult(await queryFn(...params)),
      ...options,
    });
  };
}

/**
 * Create mutation hook factory with optimistic updates
 * TCache defaults to TData and describes the values stored at the optimistic keys.
 */
export function createMutationHook<TData, TVariables, TCache = TData>(mutationFn: (variables: TVariables) => Promise<Result<TData, ApiError>>) {
  return function useApiMutation(options?: ExtendedMutationOptions<TData, TVariables, TCache>) {
    const queryClient = useQueryClient();
    const { optimistic, ...mutationOptions } = options || {};

    return useMutation<TData, ApiError, TVariables, OptimisticMutationContext | undefined>({
      ...mutationOptions,
      mutationFn: async (variables) => unwrapResult(await mutationFn(variables)),

      onMutate: async (variables: TVariables, mutationContext) => {
        /* v8 ignore start */
        if (!optimistic?.optimisticData) {
          return options?.onMutate?.(variables, mutationContext);
        }
        /* v8 ignore stop */

        // Cancel outgoing refetches
        const invalidateKeys = optimistic.invalidateKeys || [];
        await Promise.all(invalidateKeys.map((key) => queryClient.cancelQueries({ queryKey: key })));

        // Snapshot previous values
        const previousData = invalidateKeys.map((key: QueryKey) => ({
          key,
          data: queryClient.getQueryData(key),
        }));

        // Optimistically update
        invalidateKeys.forEach((key: QueryKey) => {
          const currentData = queryClient.getQueryData<TCache>(key);
          const optimisticValue = optimistic.optimisticData!(variables, currentData);
          queryClient.setQueryData(key, optimisticValue);
        });

        await options?.onMutate?.(variables, mutationContext);

        return { previousData };
      },

      onError: (error, variables, onMutateResult, mutationContext) => {
        // Rollback on error if configured
        /* v8 ignore start */
        if (optimistic?.rollbackOnError !== false && onMutateResult?.previousData) {
          /* v8 ignore stop */
          onMutateResult.previousData.forEach(({ key, data }) => {
            queryClient.setQueryData(key, data);
          });
        }

        options?.onError?.(error, variables, onMutateResult, mutationContext);
      },

      onSuccess: async (data, variables, onMutateResult, mutationContext) => {
        // Invalidate queries
        if (optimistic?.invalidateKeys) {
          await Promise.all(optimistic.invalidateKeys.map((key) => queryClient.invalidateQueries({ queryKey: key })));
        }

        // Refetch queries
        if (optimistic?.refetchKeys) {
          await Promise.all(optimistic.refetchKeys.map((key) => queryClient.refetchQueries({ queryKey: key })));
        }

        await options?.onSuccess?.(data, variables, onMutateResult, mutationContext);
      },
    });
  };
}

/**
 * Create React Query hooks for a module
 */
export interface ModuleHooks {
  queries: Record<string, ReturnType<typeof createQueryHook>>;
  mutations: Record<string, ReturnType<typeof createMutationHook>>;
}

/**
 * Generate query key from endpoint info
 */
export function generateQueryKey(module: string, operation: string, params?: Record<string, unknown>): QueryKey {
  const key: unknown[] = [module, operation];
  if (params && Object.keys(params).length > 0) {
    key.push(params);
  }
  return key as QueryKey;
}

/**
 * Example: Create hooks for a specific API client
 */
export function createApiClientHooks(client: ApiClient) {
  // This will be extended by generated code
  return {
    client,
    queryClient: useQueryClient,
  };
}
