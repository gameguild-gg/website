/**
 * React Integration
 *
 * Hooks and context for using the API client in React components.
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import { useMutation as useReactMutation, useQuery as useReactQuery, useQueryClient } from '@tanstack/react-query';
import { createContext, createElement, useContext, useEffect, useRef, type ReactNode } from 'react';

export * from './query-hooks.js';

export { SessionProvider } from './session-provider.js';
export { SessionContext } from './session-provider.js';
export type { SessionContextValue } from './session-provider.js';
export { useSession } from './use-session.js';
export { useAuth } from './use-auth.js';
export type { UseAuthReturn, MfaEnrollmentData } from './use-auth.js';
export { createAuthBroadcast } from './broadcast.js';
export type { AuthBroadcastMessage } from './broadcast.js';

export interface QueryState<T> {
  data: T | undefined;
  error: ApiError | undefined;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
}

export interface MutationState<T, TVariables> {
  data: T | undefined;
  error: ApiError | undefined;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  mutate: (variables: TVariables) => Promise<Result<T, ApiError>>;
  reset: () => void;
}

export interface UseQueryOptions<T> {
  enabled?: boolean;
  refetchInterval?: number;
  refetchOnWindowFocus?: boolean;
  retry?: number | boolean;
  onSuccess?: (data: T) => void;
  onError?: (error: ApiError) => void;
}

export interface UseMutationOptions<T, TVariables> {
  onSuccess?: (data: T, variables: TVariables) => void;
  onError?: (error: ApiError, variables: TVariables) => void;
  onSettled?: (data: T | undefined, error: ApiError | undefined, variables: TVariables) => void;
}

export type { ApiClient, Result, ApiError };

/**
 * Props for ApiClientProvider.
 */
export interface ApiClientProviderProps {
  client: ApiClient;
  children: ReactNode;
}

export const ApiClientContext = createContext<ApiClient | undefined>(undefined);
ApiClientContext.displayName = 'ApiClientContext';

export function ApiClientProvider({ client, children }: ApiClientProviderProps): ReactNode {
  return createElement(ApiClientContext.Provider, { value: client }, children);
}

function unwrapResult<T>(result: Result<T, ApiError>): T {
  if (result.ok) {
    return result.data;
  }

  throw result.error;
}

export function useApiClient(): ApiClient {
  const client = useContext(ApiClientContext);

  if (!client) {
    throw new Error('useApiClient must be used within an <ApiClientProvider>.');
  }

  return client;
}

export function useQuery<T>(_queryKey: unknown[], _queryFn: () => Promise<Result<T, ApiError>>, _options?: UseQueryOptions<T>): QueryState<T> {
  const query = useReactQuery<T, ApiError>({
    queryKey: _queryKey,
    queryFn: async () => unwrapResult(await _queryFn()),
    enabled: _options?.enabled,
    refetchInterval: _options?.refetchInterval,
    refetchOnWindowFocus: _options?.refetchOnWindowFocus,
    retry: _options?.retry,
  });

  useEffect(() => {
    if (query.isSuccess && query.data !== undefined) {
      _options?.onSuccess?.(query.data);
    }
  }, [_options, query.data, query.isSuccess]);

  useEffect(() => {
    if (query.isError && query.error) {
      _options?.onError?.(query.error);
    }
  }, [_options, query.error, query.isError]);

  return {
    data: query.data,
    error: query.error ?? undefined,
    isLoading: query.isLoading,
    isError: query.isError,
    isSuccess: query.isSuccess,
  };
}

export function useMutation<T, TVariables>(
  _mutationFn: (variables: TVariables) => Promise<Result<T, ApiError>>,
  _options?: UseMutationOptions<T, TVariables>,
): MutationState<T, TVariables> {
  const mutation = useReactMutation<T, ApiError, TVariables>({
    mutationFn: async (variables) => unwrapResult(await _mutationFn(variables)),
    onSuccess: (data, variables) => _options?.onSuccess?.(data, variables),
    onError: (error, variables) => _options?.onError?.(error, variables),
    onSettled: (data, error, variables) => _options?.onSettled?.(data, error ?? undefined, variables),
  });

  return {
    data: mutation.data,
    error: mutation.error ?? undefined,
    isLoading: mutation.isPending,
    isError: mutation.isError,
    isSuccess: mutation.isSuccess,
    mutate: async (variables) => {
      try {
        const data = await mutation.mutateAsync(variables);
        return { ok: true, data };
      } catch (error) {
        return { ok: false, error: error as ApiError };
      }
    },
    reset: mutation.reset,
  };
}

export function useOptimisticUpdate<T>(_queryKey: unknown[]): {
  update: (updater: (old: T | undefined) => T) => void;
  rollback: () => void;
  get: () => T | undefined;
} {
  const queryClient = useQueryClient();
  const previousValueRef = useRef<T | undefined>(undefined);

  return {
    update: (updater) => {
      previousValueRef.current = queryClient.getQueryData<T>(_queryKey);
      queryClient.setQueryData<T>(_queryKey, (old) => updater(old));
    },
    rollback: () => {
      if (previousValueRef.current === undefined) {
        queryClient.removeQueries({ queryKey: _queryKey, exact: true });
        return;
      }

      queryClient.setQueryData(_queryKey, previousValueRef.current);
    },
    get: () => queryClient.getQueryData<T>(_queryKey),
  };
}
