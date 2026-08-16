/**
 * Client Factory
 *
 * Creates API client instances for browser/client-side usage.
 */

import { createFetchTransport, createHeaderInterceptor } from './runtime/transport/fetch.js';
import { TokenRefreshManager } from './runtime/auth/refresh.js';
import { RequestDeduplicator } from './runtime/deduplication/index.js';
import { DevTools } from './runtime/devtools/index.js';
import { ok, err } from './runtime/result/helpers.js';
import type { Result } from './runtime/result/types.js';
import type { ApiError } from './runtime/errors/types.js';
import type { TokenProvider } from './runtime/auth/types.js';
import type { TenantProvider } from './runtime/tenant/types.js';
import type { ApiResponse, Interceptor, RequestConfig, Transport } from './runtime/transport/types.js';
import type { ApiClient } from './runtime/client.js';
import type { DeduplicationConfig } from './runtime/deduplication/index.js';
import type { DevToolsConfig } from './runtime/devtools/index.js';

/**
 * Client configuration options
 */
export interface ClientConfig {
  /** Base URL for API requests */
  baseUrl: string;

  /** Token provider for authentication */
  auth?: TokenProvider;

  /** Tenant provider for multi-tenancy */
  tenant?: TenantProvider;

  /** Default request timeout in milliseconds */
  timeout?: number;

  /** Additional interceptors */
  interceptors?: Interceptor[];

  /** Enable automatic token refresh */
  autoRefresh?: boolean;

  /** Request deduplication configuration */
  deduplication?: DeduplicationConfig;

  /** DevTools configuration */
  devtools?: DevToolsConfig;
}

/**
 * Create an API client for browser/client-side usage
 */
export function createClient(config: ClientConfig): ApiClient {
  const interceptors: Interceptor[] = [...(config.interceptors || [])];

  // Initialize deduplication
  const deduplicator = new RequestDeduplicator(config.deduplication);

  // Initialize DevTools
  const devtools = new DevTools(config.devtools);

  // Token refresh manager
  let refreshManager: TokenRefreshManager | null = null;

  // Add auth interceptor
  if (config.auth) {
    if (config.autoRefresh !== false && config.auth.getRefreshToken) {
      refreshManager = new TokenRefreshManager(config.auth, config.baseUrl);
    }

    interceptors.push(
      createHeaderInterceptor(async (): Promise<Record<string, string>> => {
        // Check if token needs refresh
        if (refreshManager) {
          await refreshManager.refreshIfNeeded();
        }

        const token = await config.auth!.getAccessToken();
        if (token) {
          return { Authorization: `Bearer ${token}` };
        }
        return {};
      }),
    );
  }

  // Add tenant interceptor
  if (config.tenant) {
    interceptors.push(
      createHeaderInterceptor(async (): Promise<Record<string, string>> => {
        const tenantId = await config.tenant!.getTenantId();
        if (tenantId) {
          return { 'X-Tenant-Id': tenantId };
        }
        return {};
      }),
    );
  }

  // Create transport
  const transport: Transport = createFetchTransport({
    baseUrl: config.baseUrl,
    timeout: config.timeout,
    interceptors,
  });

  async function performRequest<T>(
    requestConfig: RequestConfig
  ): Promise<Result<ApiResponse<T>, ApiError>> {
    // Check auth requirement
    /* v8 ignore start */
    if (requestConfig.requiresAuth && config.auth) {
      const token = await config.auth.getAccessToken();
      if (!token) {
      /* v8 ignore stop */
        await config.auth.onAuthenticationRequired?.();
        return err({
          name: 'ApiError',
          message: 'Authentication required',
          status: 401,
          code: 'TOKEN_MISSING',
        });
      }
    }

    // Deduplicate request if enabled
    const shouldDeduplicate = config.deduplication?.enabled !== false;

    const executeRequest = async (): Promise<Result<ApiResponse<T>, ApiError>> => {
      // Log request start
      devtools.logRequestStart(requestConfig);

      const result = await transport.request<T>(requestConfig);

      // Log request completion
      devtools.logRequestComplete(requestConfig, result);

      return result;
    };

    if (shouldDeduplicate && requestConfig.method === 'GET') {
      return deduplicator.deduplicate(
        /* v8 ignore start */
        requestConfig.method || 'GET',
        requestConfig.path || '',
        /* v8 ignore stop */
        requestConfig.body,
        executeRequest
      );
    }

    return executeRequest();
  }

  // Create client
  const client: ApiClient = {
    async request<T>(requestConfig: RequestConfig): Promise<Result<T, ApiError>> {
      const result = await performRequest<T>(requestConfig);

      if (result.ok) {
        return ok(result.data.data);
      }

      return result;
    },

    async requestRaw<T>(requestConfig: RequestConfig): Promise<Result<ApiResponse<T>, ApiError>> {
      return performRequest<T>(requestConfig);
    },

    getBaseUrl() {
      return config.baseUrl;
    },
  };

  return client;
}
